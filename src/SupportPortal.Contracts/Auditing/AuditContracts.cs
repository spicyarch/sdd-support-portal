namespace SupportPortal.Contracts.Auditing;

public sealed record AuditEventResponse(
    Guid AuditEventId,
    DateTimeOffset OccurredAt,
    string EventType,
    Guid? ActorUserId,
    string TargetType,
    Guid TargetId,
    string Outcome,
    string? Metadata);

public sealed record AuditEventCollectionResponse(IReadOnlyList<AuditEventResponse> Items);