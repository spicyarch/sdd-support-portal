using SupportPortal.Application.Branding;
using SupportPortal.Domain.Settings;

namespace SupportPortal.Application.Settings;

public enum RuntimeEmailAvailabilityState
{
    Disabled,
    Ready,
    InvalidConfiguration
}

public sealed record RuntimeEmailAvailability(
    RuntimeEmailAvailabilityState State,
    IReadOnlyList<string> InvalidSettingNames,
    DateTimeOffset CheckedAt)
{
    public bool CanSend => State == RuntimeEmailAvailabilityState.Ready;
}

public sealed record EffectiveSendGridSettings(
    bool Enabled,
    string? ApiKey,
    string? SenderDisplayName,
    string? SenderAddress,
    string? ReplyToAddress,
    IReadOnlyList<string> GlobalSupportRecipients,
    string? PublicPortalUrl,
    int HttpTimeoutSeconds,
    int MaximumAttempts,
    int MinimumBackoffSeconds,
    int MaximumBackoffSeconds,
    string DataResidency,
    int BatchSize,
    int LeaseSeconds);

public sealed record EffectiveSettingsSnapshot(
    string Version,
    SettingsSource Source,
    EffectiveBrandProfile Branding,
    string InvitationAcceptanceBaseUrl,
    int InvitationLifetimeHours,
    EffectiveSendGridSettings SendGrid,
    RuntimeEmailAvailability EmailAvailability,
    bool ApiKeyConfigured,
    SettingsApiKeyMode ApiKeyMode,
    DateTimeOffset LoadedAt);
