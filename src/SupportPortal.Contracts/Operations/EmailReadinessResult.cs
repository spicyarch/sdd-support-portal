namespace SupportPortal.Contracts.Operations;

public sealed record EmailReadinessResult(
    string Mode,
    string Outcome,
    string Stage,
    int? ProviderHttpStatus,
    string FailureCategory,
    DateTimeOffset CheckedAt,
    string CorrelationId,
    string DeliveryMeaning,
    IReadOnlyList<string> InvalidSettingNames);