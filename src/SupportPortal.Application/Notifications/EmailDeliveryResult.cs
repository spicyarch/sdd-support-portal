namespace SupportPortal.Application.Notifications;

public enum EmailDeliveryOutcome
{
    Accepted,
    RetryableFailure,
    PermanentFailure
}

public sealed record EmailDeliveryResult(
    EmailDeliveryOutcome Outcome,
    int? StatusCode,
    string? ProviderMessageId,
    string? FailureCategory,
    TimeSpan? RetryAfter = null,
    bool Ambiguous = false);

public enum EmailReadinessMode
{
    Sandbox,
    Live
}

public sealed record EmailReadinessRequest(
    EmailReadinessMode Mode,
    string? TestRecipient,
    bool ConfirmLiveSend);

public enum EmailReadinessOutcome
{
    Ready,
    Accepted,
    Disabled,
    InvalidConfiguration,
    ProviderRejected,
    ProviderUnavailable
}

public sealed record EmailReadinessResult(
    EmailReadinessMode Mode,
    EmailReadinessOutcome Outcome,
    string Stage,
    int? ProviderHttpStatus,
    string FailureCategory,
    DateTimeOffset CheckedAt,
    string CorrelationId,
    string DeliveryMeaning,
    IReadOnlyList<string> InvalidSettingNames);