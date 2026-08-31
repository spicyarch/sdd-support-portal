namespace SupportPortal.Contracts.Settings;

public sealed record BrandingSettings(
    string ProductName,
    string ShortProductName,
    string? LogoUrl,
    string? FaviconUrl,
    string PrimaryColor,
    string AccentColor,
    string FocusColor,
    string SupportContactName,
    string SupportContactEmail,
    string? OrganizationName);

public sealed record InvitationSettings(
    string AcceptanceBaseUrl,
    int LifetimeHours);

public sealed record SendGridSettingsView(
    bool Enabled,
    bool ApiKeyConfigured,
    string ApiKeyMode,
    string SenderDisplayName,
    string SenderAddress,
    string ReplyToAddress,
    IReadOnlyList<string> GlobalSupportRecipients,
    string PublicPortalUrl,
    int HttpTimeoutSeconds,
    int MaximumAttempts,
    int MinimumBackoffSeconds,
    int MaximumBackoffSeconds,
    string DataResidency,
    int BatchSize,
    int LeaseSeconds);

public sealed record EmailAvailability(
    string State,
    IReadOnlyList<string> InvalidSettingNames,
    DateTimeOffset CheckedAt);

public sealed record SettingsActivation(
    string State,
    string ActiveVersion,
    string DesiredVersion,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastSuccessfulAt,
    string? FailureCategory,
    string RetryState,
    IReadOnlyList<string> InvalidSettingNames);

public sealed record SettingsUpdater(
    Guid UserId,
    string DisplayName);

public sealed record GlobalSettingsResponse(
    string SettingsVersion,
    string Source,
    DateTimeOffset? UpdatedAt,
    SettingsUpdater? UpdatedBy,
    BrandingSettings Branding,
    InvitationSettings Invitation,
    SendGridSettingsView SendGrid,
    EmailAvailability EmailAvailability,
    SettingsActivation Activation);

public sealed record BrandingSettingsUpdate(
    string? ProductName,
    string? ShortProductName,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? AccentColor,
    string? FocusColor,
    string? SupportContactName,
    string? SupportContactEmail,
    string? OrganizationName);

public sealed record InvitationSettingsUpdate(
    string? AcceptanceBaseUrl,
    int? LifetimeHours);

public sealed record SendGridSettingsUpdate(
    bool Enabled,
    string? SenderDisplayName,
    string? SenderAddress,
    string? ReplyToAddress,
    IReadOnlyList<string>? GlobalSupportRecipients,
    string? PublicPortalUrl,
    int? HttpTimeoutSeconds,
    int? MaximumAttempts,
    int? MinimumBackoffSeconds,
    int? MaximumBackoffSeconds,
    string? DataResidency,
    int? BatchSize,
    int? LeaseSeconds,
    string? ApiKey = null,
    bool ClearApiKey = false);

public sealed record UpdateGlobalSettingsRequest(
    BrandingSettingsUpdate Branding,
    InvitationSettingsUpdate Invitation,
    SendGridSettingsUpdate SendGrid);
