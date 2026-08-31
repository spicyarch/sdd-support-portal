using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Settings;
using SupportPortal.Domain.Settings;

namespace SupportPortal.Infrastructure.Configuration;

public static class SettingsRuntimeRegistration
{
    public static IServiceCollection AddSettingsRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IProtectedSecretStore>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<AzureOptions>();
            var secretName = string.IsNullOrWhiteSpace(options.SendGridApiKeySecretName)
                ? "support-portal-sendgrid-api-key"
                : options.SendGridApiKeySecretName;
            if (Uri.TryCreate(options.KeyVaultUri, UriKind.Absolute, out var keyVaultUri) &&
                StringComparer.OrdinalIgnoreCase.Equals(keyVaultUri.Scheme, Uri.UriSchemeHttps))
            {
                return new KeyVaultSecretStore(new SecretClient(keyVaultUri, new DefaultAzureCredential()), secretName);
            }

            return new ConfigurationProtectedSecretStore(configuration, secretName);
        });
        services.AddSingleton(new SettingsCandidateValidator(configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production"));
        services.AddSingleton<SettingsSnapshotLoader>();
        services.AddSingleton<ISettingsSnapshotLoader>(serviceProvider =>
            serviceProvider.GetRequiredService<SettingsSnapshotLoader>());
        services.AddSingleton<RuntimeSettingsState>(serviceProvider =>
        {
            var loader = serviceProvider.GetRequiredService<SettingsSnapshotLoader>();
            return new RuntimeSettingsState(loader.CreateHostDefaults());
        });
        services.AddSingleton<SettingsRefreshCoordinator>();
        return services;
    }
}

public sealed class SettingsSnapshotLoader : ISettingsSnapshotLoader
{
    private readonly IPortalStore store;
    private readonly IProtectedSecretStore secrets;
    private readonly AzureOptions azureOptions;
    private readonly BrandingOptions brandingOptions;
    private readonly SendGridOptions sendGridOptions;
    private readonly SendGridOptionsValidator sendGridValidator = new();
    private readonly SettingsCandidateValidator candidateValidator;
    private readonly string environmentName;


    public SettingsSnapshotLoader(
        IPortalStore store,
        IProtectedSecretStore secrets,
        AzureOptions azureOptions,
        BrandingOptions brandingOptions,
        SendGridOptions sendGridOptions,
        IConfiguration configuration,
        SettingsCandidateValidator candidateValidator)
    {
        this.store = store;
        this.secrets = secrets;
        this.azureOptions = azureOptions;
        this.brandingOptions = brandingOptions;
        this.sendGridOptions = sendGridOptions;
        environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        this.candidateValidator = candidateValidator;
    }

    public Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(store.GetDeploymentSettings()?.Revision);
    }

    public async Task<EffectiveSettingsSnapshot> LoadAsync(string version, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = store.GetDeploymentSettings();
        if (settings is null)
        {
            return CreateHostDefaults();
        }

        var apiKey = await ResolveApiKeyAsync(settings, cancellationToken);
        var recipients = store.GetDeploymentSettingsRecipients(settings.DeploymentSettingsId)
            .Select(item => item.NormalizedAddress)
            .ToArray();
        var baseline = CreateBaseline(settings.SendGridApiKeyMode, apiKey is not null);
        var input = CreateUpdateRequest(settings, recipients);
        var candidate = candidateValidator.Validate(input, baseline);
        if (!candidate.IsValid)
        {
            throw new SettingsRefreshException("InvalidConfiguration", candidate.InvalidSettingNames);
        }

        var branding = BrandingResolver.Resolve(candidate.Branding, environmentName);
        var effectiveSendGrid = ToEffectiveSendGrid(candidate.SendGrid, apiKey);
        var checkedAt = DateTimeOffset.UtcNow;
        var availability = ToRuntimeAvailability(sendGridValidator.Validate(ToOptions(effectiveSendGrid), environmentName, checkedAt));
        return new EffectiveSettingsSnapshot(
            settings.Revision,
            DetermineSource(settings),
            branding,
            candidate.InvitationAcceptanceBaseUrl,
            candidate.InvitationLifetimeHours,
            effectiveSendGrid,
            availability,
            apiKey is not null,
            candidate.ApiKeyMode,
            checkedAt);
    }

    public EffectiveSettingsSnapshot CreateHostDefaults()
    {
        var now = DateTimeOffset.UtcNow;
        var branding = BrandingResolver.Resolve(
            new BrandingInput(
                brandingOptions.ProductName,
                brandingOptions.ShortProductName,
                brandingOptions.LogoUrl,
                brandingOptions.FaviconUrl,
                brandingOptions.PrimaryColor,
                brandingOptions.AccentColor,
                brandingOptions.FocusColor,
                brandingOptions.SupportContactName,
                brandingOptions.SupportContactEmail,
                brandingOptions.OrganizationName),
            environmentName);
        var sendGrid = new EffectiveSendGridSettings(
            sendGridOptions.Enabled,
            sendGridOptions.ApiKey,
            sendGridOptions.SenderDisplayName,
            sendGridOptions.SenderAddress,
            sendGridOptions.ReplyToAddress,
            sendGridOptions.GlobalSupportRecipients,
            sendGridOptions.PublicPortalUrl,
            sendGridOptions.HttpTimeoutSeconds,
            sendGridOptions.MaximumAttempts,
            sendGridOptions.MinimumBackoffSeconds,
            sendGridOptions.MaximumBackoffSeconds,
            sendGridOptions.DataResidency,
            sendGridOptions.BatchSize,
            sendGridOptions.LeaseSeconds);
        var availability = ToRuntimeAvailability(sendGridValidator.Validate(sendGridOptions, environmentName, now));
        return new EffectiveSettingsSnapshot(
            "host-defaults",
            SettingsSource.HostDefaults,
            branding,
            azureOptions.InvitationAcceptanceBaseUrl ?? "http://localhost:5258/invitations/accept",
            Math.Clamp(azureOptions.InvitationLifetimeHours, 1, 168),
            sendGrid,
            availability,
            !string.IsNullOrWhiteSpace(sendGrid.ApiKey),
            SettingsApiKeyMode.Inherit,
            now);
    }

    private async Task<string?> ResolveApiKeyAsync(DeploymentSettings settings, CancellationToken cancellationToken)
    {
        if (settings.SendGridApiKeyMode == SettingsApiKeyMode.Cleared)
        {
            return null;
        }

        if (settings.SendGridApiKeyMode == SettingsApiKeyMode.Managed)
        {
            return await secrets.GetAsync(settings.SendGridApiKeySecretVersion, cancellationToken)
                ?? throw new SettingsRefreshException("SecretProviderUnavailable", ["SendGrid:ApiKey"]);
        }

        if (!string.IsNullOrWhiteSpace(sendGridOptions.ApiKey))
        {
            return sendGridOptions.ApiKey;
        }

        return await secrets.GetAsync(null, cancellationToken);
    }

    private SettingsValidationBaseline CreateBaseline(SettingsApiKeyMode apiKeyMode, bool apiKeyConfigured) =>
        new(
            new BrandingInput(
                brandingOptions.ProductName,
                brandingOptions.ShortProductName,
                brandingOptions.LogoUrl,
                brandingOptions.FaviconUrl,
                brandingOptions.PrimaryColor,
                brandingOptions.AccentColor,
                brandingOptions.FocusColor,
                brandingOptions.SupportContactName,
                brandingOptions.SupportContactEmail,
                brandingOptions.OrganizationName),
            azureOptions.InvitationAcceptanceBaseUrl ?? "http://localhost:5258/invitations/accept",
            Math.Clamp(azureOptions.InvitationLifetimeHours, 1, 168),
            new SendGridSettingsBaseline(
                sendGridOptions.Enabled,
                sendGridOptions.SenderDisplayName,
                sendGridOptions.SenderAddress,
                sendGridOptions.ReplyToAddress,
                sendGridOptions.GlobalSupportRecipients,
                sendGridOptions.PublicPortalUrl,
                sendGridOptions.HttpTimeoutSeconds,
                sendGridOptions.MaximumAttempts,
                sendGridOptions.MinimumBackoffSeconds,
                sendGridOptions.MaximumBackoffSeconds,
                sendGridOptions.DataResidency,
                sendGridOptions.BatchSize,
                sendGridOptions.LeaseSeconds),
            apiKeyMode,
            apiKeyConfigured);

    private Contracts.Settings.UpdateGlobalSettingsRequest CreateUpdateRequest(
        DeploymentSettings settings,
        IReadOnlyList<string> recipients) =>
        new(
            new Contracts.Settings.BrandingSettingsUpdate(
                settings.ProductName,
                settings.ShortProductName,
                settings.LogoUrl,
                settings.FaviconUrl,
                settings.PrimaryColor,
                settings.AccentColor,
                settings.FocusColor,
                settings.SupportContactName,
                settings.SupportContactEmail,
                settings.OrganizationName),
            new Contracts.Settings.InvitationSettingsUpdate(
                settings.InvitationAcceptanceBaseUrl,
                settings.InvitationLifetimeHours),
            new Contracts.Settings.SendGridSettingsUpdate(
                settings.SendGridEnabled ?? sendGridOptions.Enabled,
                settings.SendGridSenderDisplayName,
                settings.SendGridSenderAddress,
                settings.SendGridReplyToAddress,
                recipients,
                settings.SendGridPublicPortalUrl,
                settings.SendGridHttpTimeoutSeconds,
                settings.SendGridMaximumAttempts,
                settings.SendGridMinimumBackoffSeconds,
                settings.SendGridMaximumBackoffSeconds,
                settings.SendGridDataResidency,
                settings.SendGridBatchSize,
                settings.SendGridLeaseSeconds,
                null,
                settings.SendGridApiKeyMode == SettingsApiKeyMode.Cleared));

    private static EffectiveSendGridSettings ToEffectiveSendGrid(
        SendGridSettingsBaseline values,
        string? apiKey) =>
        new(
            values.Enabled,
            apiKey,
            values.SenderDisplayName,
            values.SenderAddress,
            values.ReplyToAddress,
            values.GlobalSupportRecipients,
            values.PublicPortalUrl,
            values.HttpTimeoutSeconds,
            values.MaximumAttempts,
            values.MinimumBackoffSeconds,
            values.MaximumBackoffSeconds,
            values.DataResidency,
            values.BatchSize,
            values.LeaseSeconds);

    private static SendGridOptions ToOptions(EffectiveSendGridSettings values) => new()
    {
        Enabled = values.Enabled,
        ApiKey = values.ApiKey,
        SenderDisplayName = values.SenderDisplayName,
        SenderAddress = values.SenderAddress,
        ReplyToAddress = values.ReplyToAddress,
        GlobalSupportRecipients = values.GlobalSupportRecipients,
        PublicPortalUrl = values.PublicPortalUrl,
        HttpTimeoutSeconds = values.HttpTimeoutSeconds,
        MaximumAttempts = values.MaximumAttempts,
        MinimumBackoffSeconds = values.MinimumBackoffSeconds,
        MaximumBackoffSeconds = values.MaximumBackoffSeconds,
        DataResidency = values.DataResidency,
        BatchSize = values.BatchSize,
        LeaseSeconds = values.LeaseSeconds
    };

    private static RuntimeEmailAvailability ToRuntimeAvailability(EmailDeliveryAvailability availability) =>
        new(
            (RuntimeEmailAvailabilityState)availability.State,
            availability.InvalidSettingNames,
            availability.CheckedAt);

    private static SettingsSource DetermineSource(DeploymentSettings settings)
    {
        var overrideCount = new object?[]
        {
            settings.ProductName,
            settings.ShortProductName,
            settings.LogoUrl,
            settings.FaviconUrl,
            settings.PrimaryColor,
            settings.AccentColor,
            settings.FocusColor,
            settings.SupportContactName,
            settings.SupportContactEmail,
            settings.OrganizationName,
            settings.InvitationAcceptanceBaseUrl,
            settings.InvitationLifetimeHours,
            settings.SendGridEnabled,
            settings.SendGridSenderDisplayName,
            settings.SendGridSenderAddress,
            settings.SendGridReplyToAddress,
            settings.SendGridPublicPortalUrl,
            settings.SendGridHttpTimeoutSeconds,
            settings.SendGridMaximumAttempts,
            settings.SendGridMinimumBackoffSeconds,
            settings.SendGridMaximumBackoffSeconds,
            settings.SendGridDataResidency,
            settings.SendGridBatchSize,
            settings.SendGridLeaseSeconds
        }.Count(value => value is not null);
        return overrideCount == 24 ? SettingsSource.AdministratorOverride : SettingsSource.Mixed;
    }
}
