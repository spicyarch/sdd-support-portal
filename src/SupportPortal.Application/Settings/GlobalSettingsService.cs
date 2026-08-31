using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Commands;
using SupportPortal.Application.Common;
using SupportPortal.Contracts.Settings;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Settings;

namespace SupportPortal.Application.Settings;

public sealed class GlobalSettingsService
{
    private readonly IPortalStore store;
    private readonly IProtectedSecretStore secrets;
    private readonly RuntimeSettingsState runtimeSettings;
    private readonly SettingsRefreshCoordinator refreshCoordinator;
    private readonly SettingsCandidateValidator candidateValidator;
    private readonly IdempotencyService idempotency;
    private readonly TimeProvider clock;
    private readonly PortalAccessEvaluator access = new();

    public GlobalSettingsService(
        IPortalStore store,
        IProtectedSecretStore secrets,
        RuntimeSettingsState runtimeSettings,
        SettingsRefreshCoordinator refreshCoordinator,
        SettingsCandidateValidator candidateValidator,
        TimeProvider clock)
    {
        this.store = store;
        this.secrets = secrets;
        this.runtimeSettings = runtimeSettings;
        this.refreshCoordinator = refreshCoordinator;
        this.candidateValidator = candidateValidator;
        idempotency = new IdempotencyService(store);
        this.clock = clock;
    }

    public async Task<GlobalSettingsResponse> GetAsync(
        PortalPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        EnsureSettingsAccess(principal);
        await refreshCoordinator.RefreshIfDueAsync(cancellationToken);
        return MapResponse();
    }

    public async Task<GlobalSettingsResponse> ReplaceAsync(
        PortalPrincipal principal,
        string expectedVersion,
        Guid idempotencyKey,
        UpdateGlobalSettingsRequest input,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureSettingsAccess(principal);
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(expectedVersion))
        {
            throw new PortalServiceException(400, "Invalid settings version", "A settings version is required.");
        }

        var fingerprint = IdempotencyService.FingerprintSettings(input);
        var existingReceipt = idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out GlobalSettingsResponse? replay);
        if (existingReceipt && replay is not null)
        {
            return replay;
        }

        var current = runtimeSettings.Current;
        var baseline = CreateBaseline(current);
        var candidate = candidateValidator.Validate(input, baseline);
        if (!candidate.IsValid)
        {
            RecordRejected(
                principal,
                "ValidationFailed",
                runtimeSettings.Current.Version,
                candidate.InvalidSettingNames,
                correlationId);
            throw new PortalServiceException(
                400,
                "Validation failed",
                $"Invalid settings: {string.Join(", ", candidate.InvalidSettingNames)}.");
        }

        var currentSettings = store.GetDeploymentSettings();
        var currentVersion = currentSettings?.Revision ?? runtimeSettings.Current.Version;
        if (!StringComparer.Ordinal.Equals(expectedVersion.Trim('"'), currentVersion.Trim('"')))
        {
            RecordRejected(principal, "Conflict", currentVersion, GetChangedSettingNames(input), correlationId);
            throw new PortalServiceException(412, "Settings changed", "The settings changed after this page loaded. Reload the current settings and try again.");
        }

        ProtectedSecretReference? stagedSecret = null;
        if (candidate.ApiKeyMode == SettingsApiKeyMode.Managed &&
            !string.IsNullOrWhiteSpace(candidate.ReplacementApiKey))
        {
            try
            {
                stagedSecret = await secrets.SetAsync(candidate.ReplacementApiKey, cancellationToken);
            }
            catch (ProtectedSecretStoreException)
            {
                RecordRejected(principal, "SecretProviderUnavailable", currentVersion, ["SendGrid:ApiKey"], correlationId);
                throw new PortalServiceException(503, "Secret provider unavailable", "The settings could not be saved because protected secret storage is unavailable.");
            }
        }

        var now = clock.GetUtcNow();
        var revision = Guid.NewGuid().ToString("N");
        var settingsId = Guid.NewGuid();
        var effectiveStoredSecretVersion = candidate.ApiKeyMode == SettingsApiKeyMode.Managed
            ? stagedSecret?.Version ?? store.GetDeploymentSettings()?.SendGridApiKeySecretVersion
            : null;
        var values = ToValues(candidate, effectiveStoredSecretVersion);
        GlobalSettingsResponse? response = null;

        try
        {
            store.Execute(() =>
            {
                if (idempotency.TryReplay(principal.UserId, idempotencyKey, fingerprint, out GlobalSettingsResponse? concurrentReplay) && concurrentReplay is not null)
                {
                    response = concurrentReplay;
                    return;
                }

                var settings = store.GetDeploymentSettings();
                var actualVersion = settings?.Revision ?? runtimeSettings.Current.Version;
                if (!StringComparer.Ordinal.Equals(expectedVersion.Trim('"'), actualVersion.Trim('"')))
                {
                    throw new PortalServiceException(412, "Settings changed", "The settings changed after this page loaded. Reload the current settings and try again.");
                }

                if (settings is null)
                {
                    settings = new DeploymentSettings(settingsId, now, principal.UserId, revision, values);
                    store.AddDeploymentSettings(settings);
                }
                else
                {
                    settings.Replace(values, revision, now, principal.UserId);
                    settingsId = settings.DeploymentSettingsId;
                    store.RemoveDeploymentSettingsRecipients(settings.DeploymentSettingsId);
                }

                foreach (var address in candidate.SendGrid.GlobalSupportRecipients)
                {
                    store.AddDeploymentSettingsRecipient(new DeploymentSettingsRecipient(Guid.NewGuid(), settingsId, address, now));
                }

                var changedNames = GetChangedSettingNames(input);
                store.AddAuditEvent(new AuditEvent(
                    Guid.NewGuid(),
                    now,
                    SettingsAuditPolicy.SettingsSaved,
                    principal.UserId,
                    SettingsAuditPolicy.TargetType,
                    settingsId,
                    true,
                    SettingsAuditPolicy.CreateSettingsMetadata(
                        SettingsAuditPolicy.SettingsSaved,
                        "Succeeded",
                        revision,
                        changedNames,
                        correlationId)));

                var keyAuditEvent = GetApiKeyAuditEvent(candidate);
                if (keyAuditEvent is not null)
                {
                    store.AddAuditEvent(new AuditEvent(
                        Guid.NewGuid(),
                        now,
                        keyAuditEvent,
                        principal.UserId,
                        SettingsAuditPolicy.TargetType,
                        settingsId,
                        true,
                        SettingsAuditPolicy.CreateSettingsMetadata(
                            keyAuditEvent,
                            "Succeeded",
                            revision,
                            ["SendGrid:ApiKey"],
                            correlationId)));
                }

                var provisionalSnapshot = CreateProvisionalSnapshot(candidate, revision, now);
                response = MapResponse(provisionalSnapshot, settings);
                store.AddCommandReceipt(idempotency.CreateReceipt(principal.UserId, idempotencyKey, fingerprint, 200, response, now));
            });
        }
        catch (PortalServiceException exception) when (exception.StatusCode == 412)
        {
            RecordRejected(principal, "Conflict", currentVersion, GetChangedSettingNames(input), correlationId);
            throw;
        }

        await refreshCoordinator.RefreshNowAsync(cancellationToken);
        return response ?? throw new PortalServiceException(500, "Settings save failed", "The settings could not be saved.");
    }

    private EffectiveSettingsSnapshot CreateProvisionalSnapshot(
        ValidatedSettingsCandidate candidate,
        string revision,
        DateTimeOffset loadedAt)
    {
        var previous = runtimeSettings.Current;
        var apiKey = candidate.ApiKeyMode switch
        {
            SettingsApiKeyMode.Cleared => null,
            SettingsApiKeyMode.Managed when !string.IsNullOrWhiteSpace(candidate.ReplacementApiKey) => candidate.ReplacementApiKey,
            _ => previous.SendGrid.ApiKey
        };
        var apiKeyConfigured = apiKey is not null || candidate.ApiKeyMode == SettingsApiKeyMode.Managed;
        var availability = candidate.SendGrid.Enabled
            ? new RuntimeEmailAvailability(RuntimeEmailAvailabilityState.Ready, [], loadedAt)
            : new RuntimeEmailAvailability(RuntimeEmailAvailabilityState.Disabled, [], loadedAt);
        return new EffectiveSettingsSnapshot(
            revision,
            SettingsSource.AdministratorOverride,
            BrandingResolver.Resolve(candidate.Branding, candidateValidator.EnvironmentName),
            candidate.InvitationAcceptanceBaseUrl,
            candidate.InvitationLifetimeHours,
            new EffectiveSendGridSettings(
                candidate.SendGrid.Enabled,
                apiKey,
                candidate.SendGrid.SenderDisplayName,
                candidate.SendGrid.SenderAddress,
                candidate.SendGrid.ReplyToAddress,
                candidate.SendGrid.GlobalSupportRecipients,
                candidate.SendGrid.PublicPortalUrl,
                candidate.SendGrid.HttpTimeoutSeconds,
                candidate.SendGrid.MaximumAttempts,
                candidate.SendGrid.MinimumBackoffSeconds,
                candidate.SendGrid.MaximumBackoffSeconds,
                candidate.SendGrid.DataResidency,
                candidate.SendGrid.BatchSize,
                candidate.SendGrid.LeaseSeconds),
            availability,
            apiKeyConfigured,
            candidate.ApiKeyMode,
            loadedAt);
    }

    private GlobalSettingsResponse MapResponse()
    {
        var settings = store.GetDeploymentSettings();
        return MapResponse(runtimeSettings.Current, settings);
    }

    private GlobalSettingsResponse MapResponse(
        EffectiveSettingsSnapshot snapshot,
        DeploymentSettings? settings)
    {
        var activation = runtimeSettings.Activation;
        var version = settings?.Revision ?? snapshot.Version;
        var updatedBy = settings?.UpdatedByUserId is Guid userId
            ? store.GetUser(userId) is { } user
                ? new SettingsUpdater(user.UserId, user.DisplayName)
                : null
            : null;
        return new GlobalSettingsResponse(
            version,
            snapshot.Source.ToString(),
            settings?.UpdatedAt,
            updatedBy,
            new BrandingSettings(
                snapshot.Branding.ProductName,
                snapshot.Branding.ShortProductName,
                snapshot.Branding.LogoUrl,
                snapshot.Branding.FaviconUrl,
                snapshot.Branding.PrimaryColor,
                snapshot.Branding.AccentColor,
                snapshot.Branding.FocusColor,
                snapshot.Branding.SupportContactName,
                snapshot.Branding.SupportContactEmail,
                snapshot.Branding.OrganizationName),
            new InvitationSettings(snapshot.InvitationAcceptanceBaseUrl, snapshot.InvitationLifetimeHours),
            new SendGridSettingsView(
                snapshot.SendGrid.Enabled,
                snapshot.ApiKeyConfigured,
                snapshot.ApiKeyMode.ToString(),
                snapshot.SendGrid.SenderDisplayName ?? string.Empty,
                snapshot.SendGrid.SenderAddress ?? string.Empty,
                snapshot.SendGrid.ReplyToAddress ?? string.Empty,
                snapshot.SendGrid.GlobalSupportRecipients,
                snapshot.SendGrid.PublicPortalUrl ?? string.Empty,
                snapshot.SendGrid.HttpTimeoutSeconds,
                snapshot.SendGrid.MaximumAttempts,
                snapshot.SendGrid.MinimumBackoffSeconds,
                snapshot.SendGrid.MaximumBackoffSeconds,
                snapshot.SendGrid.DataResidency,
                snapshot.SendGrid.BatchSize,
                snapshot.SendGrid.LeaseSeconds),
            new EmailAvailability(
                snapshot.EmailAvailability.State.ToString(),
                snapshot.EmailAvailability.InvalidSettingNames,
                snapshot.EmailAvailability.CheckedAt),
            new SettingsActivation(
                activation.State.ToString(),
                activation.ActiveVersion,
                activation.DesiredVersion,
                activation.LastAttemptAt,
                activation.LastSuccessfulAt,
                activation.FailureCategory,
                GetRetryState(activation.State),
                activation.InvalidSettingNames));
    }

    private static string GetRetryState(SettingsActivationState state) => state switch
    {
        SettingsActivationState.Refreshing => "InProgress",
        SettingsActivationState.ActivationFailed => "Scheduled",
        _ => "NotRequired"
    };

    private SettingsValidationBaseline CreateBaseline(EffectiveSettingsSnapshot snapshot) => new(
        new BrandingInput(
            snapshot.Branding.ProductName,
            snapshot.Branding.ShortProductName,
            snapshot.Branding.LogoUrl,
            snapshot.Branding.FaviconUrl,
            snapshot.Branding.PrimaryColor,
            snapshot.Branding.AccentColor,
            snapshot.Branding.FocusColor,
            snapshot.Branding.SupportContactName,
            snapshot.Branding.SupportContactEmail,
            snapshot.Branding.OrganizationName),
        snapshot.InvitationAcceptanceBaseUrl,
        snapshot.InvitationLifetimeHours,
        new SendGridSettingsBaseline(
            snapshot.SendGrid.Enabled,
            snapshot.SendGrid.SenderDisplayName,
            snapshot.SendGrid.SenderAddress,
            snapshot.SendGrid.ReplyToAddress,
            snapshot.SendGrid.GlobalSupportRecipients,
            snapshot.SendGrid.PublicPortalUrl,
            snapshot.SendGrid.HttpTimeoutSeconds,
            snapshot.SendGrid.MaximumAttempts,
            snapshot.SendGrid.MinimumBackoffSeconds,
            snapshot.SendGrid.MaximumBackoffSeconds,
            snapshot.SendGrid.DataResidency,
            snapshot.SendGrid.BatchSize,
            snapshot.SendGrid.LeaseSeconds),
        snapshot.ApiKeyMode,
        snapshot.ApiKeyConfigured);

    private static DeploymentSettingsValues ToValues(
        ValidatedSettingsCandidate candidate,
        string? secretVersion) => new(
        candidate.Branding.ProductName,
        candidate.Branding.ShortProductName,
        candidate.Branding.LogoUrl,
        candidate.Branding.FaviconUrl,
        candidate.Branding.PrimaryColor,
        candidate.Branding.AccentColor,
        candidate.Branding.FocusColor,
        candidate.Branding.SupportContactName,
        candidate.Branding.SupportContactEmail,
        candidate.Branding.OrganizationName,
        candidate.InvitationAcceptanceBaseUrl,
        candidate.InvitationLifetimeHours,
        candidate.SendGrid.Enabled,
        candidate.SendGrid.SenderDisplayName,
        candidate.SendGrid.SenderAddress,
        candidate.SendGrid.ReplyToAddress,
        candidate.SendGrid.PublicPortalUrl,
        candidate.SendGrid.HttpTimeoutSeconds,
        candidate.SendGrid.MaximumAttempts,
        candidate.SendGrid.MinimumBackoffSeconds,
        candidate.SendGrid.MaximumBackoffSeconds,
        candidate.SendGrid.DataResidency,
        candidate.SendGrid.BatchSize,
        candidate.SendGrid.LeaseSeconds,
        candidate.ApiKeyMode,
        secretVersion);

    private static IReadOnlyList<string> GetChangedSettingNames(UpdateGlobalSettingsRequest input)
    {
        var changed = new List<string>();
        if (input.Branding.ProductName is not null) changed.Add("Branding:ProductName");
        if (input.Branding.ShortProductName is not null) changed.Add("Branding:ShortProductName");
        if (input.Branding.LogoUrl is not null) changed.Add("Branding:LogoUrl");
        if (input.Branding.FaviconUrl is not null) changed.Add("Branding:FaviconUrl");
        if (input.Branding.PrimaryColor is not null) changed.Add("Branding:PrimaryColor");
        if (input.Branding.AccentColor is not null) changed.Add("Branding:AccentColor");
        if (input.Branding.FocusColor is not null) changed.Add("Branding:FocusColor");
        if (input.Branding.SupportContactName is not null) changed.Add("Branding:SupportContactName");
        if (input.Branding.SupportContactEmail is not null) changed.Add("Branding:SupportContactEmail");
        if (input.Branding.OrganizationName is not null) changed.Add("Branding:OrganizationName");
        if (input.Invitation.AcceptanceBaseUrl is not null) changed.Add("Portal:InvitationAcceptanceBaseUrl");
        if (input.Invitation.LifetimeHours is not null) changed.Add("Portal:InvitationLifetimeHours");
        changed.Add("SendGrid:Enabled");
        if (input.SendGrid.SenderDisplayName is not null) changed.Add("SendGrid:SenderDisplayName");
        if (input.SendGrid.SenderAddress is not null) changed.Add("SendGrid:SenderAddress");
        if (input.SendGrid.ReplyToAddress is not null) changed.Add("SendGrid:ReplyToAddress");
        if (input.SendGrid.GlobalSupportRecipients is not null) changed.Add("SendGrid:GlobalSupportRecipients");
        if (input.SendGrid.PublicPortalUrl is not null) changed.Add("SendGrid:PublicPortalUrl");
        if (input.SendGrid.ApiKey is not null || input.SendGrid.ClearApiKey) changed.Add("SendGrid:ApiKey");
        return changed;
    }

    private static string? GetApiKeyAuditEvent(ValidatedSettingsCandidate candidate) =>
        candidate.ApiKeyMode == SettingsApiKeyMode.Cleared
            ? SettingsAuditPolicy.ApiKeyCleared
            : candidate.ApiKeyMode == SettingsApiKeyMode.Managed && !string.IsNullOrWhiteSpace(candidate.ReplacementApiKey)
                ? SettingsAuditPolicy.ApiKeyReplaced
                : null;

    private void RecordRejected(
        PortalPrincipal principal,
        string outcome,
        string? revision,
        IEnumerable<string>? settingNames,
        string? correlationId)
    {
        var settingsId = store.GetDeploymentSettings()?.DeploymentSettingsId ?? Guid.Empty;
        var audit = new AuditEvent(
            Guid.NewGuid(),
            clock.GetUtcNow(),
            SettingsAuditPolicy.SettingsSaveRejected,
            principal.UserId,
            SettingsAuditPolicy.TargetType,
            settingsId,
            false,
            SettingsAuditPolicy.CreateSettingsMetadata(
                SettingsAuditPolicy.SettingsSaveRejected,
                outcome,
                revision,
                settingNames,
                correlationId));
        store.Execute(() => store.AddAuditEvent(audit));
    }

    private void EnsureSettingsAccess(PortalPrincipal principal)
    {
        if (!access.CanManageSettings(principal))
        {
            throw new PortalServiceException(403, "Forbidden", "Only an active Global Administrator can manage deployment settings.");
        }
    }
}
