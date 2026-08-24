using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.Notifications;

public sealed class Notification
{
    private Notification()
    {
    }

    public Notification(
        Guid notificationId,
        NotificationEventType eventType,
        Guid sourceEntityId,
        Guid? supportRequestId,
        Guid? invitationId,
        Guid actorUserId,
        Guid? assigneeUserIdAtEvent,
        DateTimeOffset eventOccurredAt,
        string correlationId)
    {
        var isInvitation = eventType == NotificationEventType.InvitationCreated;
        if (isInvitation == (invitationId is null) ||
            (!isInvitation && supportRequestId is null) ||
            (isInvitation && supportRequestId is not null))
        {
            throw new DomainException("Notification source context is invalid.");
        }

        if (eventType != NotificationEventType.TeamReply && assigneeUserIdAtEvent is not null)
        {
            throw new DomainException("Only team replies can snapshot an assignee.");
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new DomainException("Notification correlation is required.");
        }

        NotificationId = notificationId;
        EventType = eventType;
        SourceEntityId = sourceEntityId;
        SupportRequestId = supportRequestId;
        InvitationId = invitationId;
        ActorUserId = actorUserId;
        AssigneeUserIdAtEvent = assigneeUserIdAtEvent;
        EventOccurredAt = eventOccurredAt;
        CreatedAt = eventOccurredAt;
        CorrelationId = correlationId.Trim();
    }

    public Guid NotificationId { get; private set; }

    public NotificationEventType EventType { get; private set; }

    public Guid SourceEntityId { get; private set; }

    public Guid? SupportRequestId { get; private set; }

    public Guid? InvitationId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public Guid? AssigneeUserIdAtEvent { get; private set; }

    public DateTimeOffset EventOccurredAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public NotificationStatus Status { get; private set; } = NotificationStatus.PendingRecipients;

    public int RecipientCount { get; private set; }

    public DateTimeOffset? RecipientsExpandedAt { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public string RowVersion { get; private set; } = "1";

    public void MarkRecipientsExpanded(int recipientCount, DateTimeOffset at)
    {
        if (Status != NotificationStatus.PendingRecipients)
        {
            return;
        }

        if (recipientCount < 0)
        {
            throw new DomainException("Notification recipient count cannot be negative.");
        }

        RecipientCount = recipientCount;
        RecipientsExpandedAt = at;
        Status = recipientCount == 0 ? NotificationStatus.Suppressed : NotificationStatus.Active;
        Touch();
    }

    public void Reconcile(IReadOnlyCollection<NotificationDelivery> deliveries)
    {
        if (Status is NotificationStatus.PendingRecipients or NotificationStatus.Suppressed)
        {
            return;
        }

        if (deliveries.Count == 0)
        {
            Status = NotificationStatus.Suppressed;
        }
        else if (deliveries.All(delivery => delivery.State is NotificationDeliveryState.Sent or NotificationDeliveryState.Suppressed))
        {
            Status = NotificationStatus.Completed;
        }
        else if (deliveries.All(delivery => delivery.State is NotificationDeliveryState.Sent or NotificationDeliveryState.Suppressed or NotificationDeliveryState.PermanentFailure))
        {
            Status = NotificationStatus.CompletedWithFailure;
        }

        Touch();
    }

    private void Touch()
    {
        RowVersion = Guid.NewGuid().ToString("N");
    }
}