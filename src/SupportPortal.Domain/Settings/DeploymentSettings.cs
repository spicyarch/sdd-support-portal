using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.Settings;

public sealed record DeploymentSettingsValues(
    string? ProductName,
    string? ShortProductName,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? AccentColor,
    string? FocusColor,
    string? SupportContactName,
    string? SupportContactEmail,
    string? OrganizationName,
    string? InvitationAcceptanceBaseUrl,
    int? InvitationLifetimeHours,
    bool? SendGridEnabled,
    string? SendGridSenderDisplayName,
    string? SendGridSenderAddress,
    string? SendGridReplyToAddress,
    string? SendGridPublicPortalUrl,
    int? SendGridHttpTimeoutSeconds,
    int? SendGridMaximumAttempts,
    int? SendGridMinimumBackoffSeconds,
    int? SendGridMaximumBackoffSeconds,
    string? SendGridDataResidency,
    int? SendGridBatchSize,
    int? SendGridLeaseSeconds,
    SettingsApiKeyMode SendGridApiKeyMode,
    string? SendGridApiKeySecretVersion);

public sealed class DeploymentSettings
{
    private DeploymentSettings()
    {
    }

    public DeploymentSettings(
        Guid deploymentSettingsId,
        DateTimeOffset updatedAt,
        Guid updatedByUserId,
        string revision,
        DeploymentSettingsValues values)
    {
        DeploymentSettingsId = deploymentSettingsId;
        Apply(values);
        SetRevision(revision);
        UpdatedAt = updatedAt;
        UpdatedByUserId = updatedByUserId;
    }

    public Guid DeploymentSettingsId { get; private set; }

    public string ScopeKey { get; private set; } = "global";

    public string Revision { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public string? ProductName { get; private set; }

    public string? ShortProductName { get; private set; }

    public string? LogoUrl { get; private set; }

    public string? FaviconUrl { get; private set; }

    public string? PrimaryColor { get; private set; }

    public string? AccentColor { get; private set; }

    public string? FocusColor { get; private set; }

    public string? SupportContactName { get; private set; }

    public string? SupportContactEmail { get; private set; }

    public string? OrganizationName { get; private set; }

    public string? InvitationAcceptanceBaseUrl { get; private set; }

    public int? InvitationLifetimeHours { get; private set; }

    public bool? SendGridEnabled { get; private set; }

    public string? SendGridSenderDisplayName { get; private set; }

    public string? SendGridSenderAddress { get; private set; }

    public string? SendGridReplyToAddress { get; private set; }

    public string? SendGridPublicPortalUrl { get; private set; }

    public int? SendGridHttpTimeoutSeconds { get; private set; }

    public int? SendGridMaximumAttempts { get; private set; }

    public int? SendGridMinimumBackoffSeconds { get; private set; }

    public int? SendGridMaximumBackoffSeconds { get; private set; }

    public string? SendGridDataResidency { get; private set; }

    public int? SendGridBatchSize { get; private set; }

    public int? SendGridLeaseSeconds { get; private set; }

    public SettingsApiKeyMode SendGridApiKeyMode { get; private set; }

    public string? SendGridApiKeySecretVersion { get; private set; }

    public string RowVersion { get; private set; } = "1";

    public void Replace(
        DeploymentSettingsValues values,
        string revision,
        DateTimeOffset updatedAt,
        Guid updatedByUserId)
    {
        Apply(values);
        SetRevision(revision);
        UpdatedAt = updatedAt;
        UpdatedByUserId = updatedByUserId;
        RowVersion = Guid.NewGuid().ToString("N");
    }

    private void Apply(DeploymentSettingsValues values)
    {
        if (values.SendGridApiKeyMode == SettingsApiKeyMode.Managed &&
            string.IsNullOrWhiteSpace(values.SendGridApiKeySecretVersion))
        {
            throw new DomainException("A managed SendGrid API key requires a secret version.");
        }

        if (values.SendGridApiKeyMode != SettingsApiKeyMode.Managed &&
            !string.IsNullOrWhiteSpace(values.SendGridApiKeySecretVersion))
        {
            throw new DomainException("Only a managed SendGrid API key may have a secret version.");
        }

        ProductName = values.ProductName;
        ShortProductName = values.ShortProductName;
        LogoUrl = values.LogoUrl;
        FaviconUrl = values.FaviconUrl;
        PrimaryColor = values.PrimaryColor;
        AccentColor = values.AccentColor;
        FocusColor = values.FocusColor;
        SupportContactName = values.SupportContactName;
        SupportContactEmail = values.SupportContactEmail;
        OrganizationName = values.OrganizationName;
        InvitationAcceptanceBaseUrl = values.InvitationAcceptanceBaseUrl;
        InvitationLifetimeHours = values.InvitationLifetimeHours;
        SendGridEnabled = values.SendGridEnabled;
        SendGridSenderDisplayName = values.SendGridSenderDisplayName;
        SendGridSenderAddress = values.SendGridSenderAddress;
        SendGridReplyToAddress = values.SendGridReplyToAddress;
        SendGridPublicPortalUrl = values.SendGridPublicPortalUrl;
        SendGridHttpTimeoutSeconds = values.SendGridHttpTimeoutSeconds;
        SendGridMaximumAttempts = values.SendGridMaximumAttempts;
        SendGridMinimumBackoffSeconds = values.SendGridMinimumBackoffSeconds;
        SendGridMaximumBackoffSeconds = values.SendGridMaximumBackoffSeconds;
        SendGridDataResidency = values.SendGridDataResidency;
        SendGridBatchSize = values.SendGridBatchSize;
        SendGridLeaseSeconds = values.SendGridLeaseSeconds;
        SendGridApiKeyMode = values.SendGridApiKeyMode;
        SendGridApiKeySecretVersion = values.SendGridApiKeySecretVersion;
    }

    private void SetRevision(string revision)
    {
        if (string.IsNullOrWhiteSpace(revision))
        {
            throw new DomainException("Settings revision is required.");
        }

        Revision = revision.Trim();
    }
}

public sealed class DeploymentSettingsRecipient
{
    private DeploymentSettingsRecipient()
    {
    }

    public DeploymentSettingsRecipient(
        Guid deploymentSettingsRecipientId,
        Guid deploymentSettingsId,
        string normalizedAddress,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            throw new DomainException("A settings recipient address is required.");
        }

        DeploymentSettingsRecipientId = deploymentSettingsRecipientId;
        DeploymentSettingsId = deploymentSettingsId;
        NormalizedAddress = normalizedAddress.Trim();
        CreatedAt = createdAt;
    }

    public Guid DeploymentSettingsRecipientId { get; private set; }

    public Guid DeploymentSettingsId { get; private set; }

    public string NormalizedAddress { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
