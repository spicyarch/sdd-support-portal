namespace SupportPortal.Domain.Auditing;

public sealed record CommandReceipt(
    Guid CommandReceiptId,
    Guid ActorUserId,
    Guid IdempotencyKey,
    string RequestFingerprint,
    int ResponseStatus,
    string ResponseBody,
    DateTimeOffset CreatedAt);