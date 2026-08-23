namespace SupportPortal.Domain.Auditing;

public sealed class AuditEvent
{
    public AuditEvent(
        Guid auditEventId,
        DateTimeOffset occurredAt,
        string eventType,
        Guid? actorUserId,
        string targetType,
        Guid targetId,
        bool succeeded,
        string? metadata = null)
    {
        AuditEventId = auditEventId;
        OccurredAt = occurredAt;
        EventType = eventType;
        ActorUserId = actorUserId;
        TargetType = targetType;
        TargetId = targetId;
        Succeeded = succeeded;
        Metadata = metadata;
    }

    public Guid AuditEventId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string EventType { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public bool Succeeded { get; private set; }

    public string? Metadata { get; private set; }
}