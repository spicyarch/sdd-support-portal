using System.Text.Json;
using SupportPortal.Application.Commands;
using SupportPortal.Application.Notifications;
using SupportPortal.Contracts.Settings;

namespace SupportPortal.Application.Settings;

public static class SettingsAuditPolicy
{
    public const string TargetType = "DeploymentSettings";
    public const string SettingsRead = "SettingsRead";
    public const string SettingsSaved = "SettingsSaved";
    public const string SettingsSaveRejected = "SettingsSaveRejected";
    public const string ApiKeyReplaced = "ApiKeyReplaced";
    public const string ApiKeyCleared = "ApiKeyCleared";
    public const string EmailReadinessChecked = "EmailReadinessChecked";

    private static readonly HashSet<string> AllowedSettingNames = new(StringComparer.Ordinal)
    {
        "Branding:ProductName",
        "Branding:ShortProductName",
        "Branding:LogoUrl",
        "Branding:FaviconUrl",
        "Branding:PrimaryColor",
        "Branding:AccentColor",
        "Branding:FocusColor",
        "Branding:SupportContactName",
        "Branding:SupportContactEmail",
        "Branding:OrganizationName",
        "Portal:InvitationAcceptanceBaseUrl",
        "Portal:InvitationLifetimeHours",
        "SendGrid:Enabled",
        "SendGrid:ApiKey",
        "SendGrid:SenderDisplayName",
        "SendGrid:SenderAddress",
        "SendGrid:ReplyToAddress",
        "SendGrid:GlobalSupportRecipients",
        "SendGrid:PublicPortalUrl",
        "SendGrid:HttpTimeoutSeconds",
        "SendGrid:MaximumAttempts",
        "SendGrid:MinimumBackoffSeconds",
        "SendGrid:MaximumBackoffSeconds",
        "SendGrid:DataResidency",
        "SendGrid:BatchSize",
        "SendGrid:LeaseSeconds"
    };

    public static IReadOnlyList<string> FilterSettingNames(IEnumerable<string>? names) =>
        (names ?? [])
            .Where(AllowedSettingNames.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    public static string CreateSettingsMetadata(
        string operation,
        string outcome,
        string? revision,
        IEnumerable<string>? settingNames,
        string? correlationId = null) =>
        JsonSerializer.Serialize(new
        {
            operation,
            outcome,
            revision,
            changedSettingNames = FilterSettingNames(settingNames),
            correlationId
        });

    public static string CreateReadinessMetadata(
        EmailReadinessResult result,
        string? correlationId = null) =>
        JsonSerializer.Serialize(new
        {
            operation = EmailReadinessChecked,
            mode = result.Mode.ToString(),
            outcome = result.Outcome.ToString(),
            result.Stage,
            result.ProviderHttpStatus,
            result.FailureCategory,
            result.DeliveryMeaning,
            result.CheckedAt,
            correlationId,
            invalidSettingNames = FilterSettingNames(result.InvalidSettingNames)
        });

    public static string CreateSettingsFingerprint(UpdateGlobalSettingsRequest request)
        => IdempotencyService.FingerprintSettings(request);
}
