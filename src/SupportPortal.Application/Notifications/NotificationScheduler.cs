using System.Text.Json;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Settings;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Notifications;
using SupportPortal.Domain.SupportRequests;

namespace SupportPortal.Application.Notifications;

public sealed class NotificationScheduler
{
    private readonly IPortalStore store;
    private readonly bool? configuredEnabled;
    private readonly RuntimeSettingsState? runtimeSettings;

    public NotificationScheduler(IPortalStore store, bool enabled)
    {
        this.store = store;
        configuredEnabled = enabled;
    }

    public NotificationScheduler(IPortalStore store, RuntimeSettingsState runtimeSettings)
    {
        this.store = store;
        this.runtimeSettings = runtimeSettings;
    }

    public void ScheduleRequestCreated(SupportRequest request, Guid actorUserId, DateTimeOffset occurredAt)
    {
        if (!IsEnabled || store.GetNotification(NotificationEventType.RequestCreated, request.SupportRequestId) is not null)
        {
            return;
        }

        var notification = new Notification(
            Guid.NewGuid(),
            NotificationEventType.RequestCreated,
            request.SupportRequestId,
            request.SupportRequestId,
            null,
            actorUserId,
            null,
            occurredAt,
            Guid.NewGuid().ToString("N"));
        AddScheduled(notification, occurredAt);
    }

    public void ScheduleMessage(SupportRequest request, Message message, Guid actorUserId, DateTimeOffset occurredAt)
    {
        if (!IsEnabled || store.GetNotification(GetMessageEventType(message.AuthorRole), message.MessageId) is not null)
        {
            return;
        }

        var eventType = GetMessageEventType(message.AuthorRole);
        var notification = new Notification(
            Guid.NewGuid(),
            eventType,
            message.MessageId,
            request.SupportRequestId,
            null,
            actorUserId,
            eventType == NotificationEventType.TeamReply ? request.AssignedToUserId : null,
            occurredAt,
            Guid.NewGuid().ToString("N"));
        AddScheduled(notification, occurredAt);
    }

    public void ScheduleInvitation(Invitation invitation, Guid actorUserId, DateTimeOffset occurredAt)
    {
        if (!IsEnabled || store.GetNotification(NotificationEventType.InvitationCreated, invitation.InvitationId) is not null)
        {
            return;
        }

        var notification = new Notification(
            Guid.NewGuid(),
            NotificationEventType.InvitationCreated,
            invitation.InvitationId,
            null,
            invitation.InvitationId,
            actorUserId,
            null,
            occurredAt,
            Guid.NewGuid().ToString("N"));
        AddScheduled(notification, occurredAt);
    }

    private void AddScheduled(Notification notification, DateTimeOffset occurredAt)
    {
        store.AddNotification(notification);
        store.AddAuditEvent(new AuditEvent(
            Guid.NewGuid(),
            occurredAt,
            "NotificationScheduled",
            null,
            "Notification",
            notification.NotificationId,
            true,
            JsonSerializer.Serialize(new
            {
                notificationId = notification.NotificationId,
                sourceEventType = notification.EventType.ToString(),
                sourceEntityId = notification.SourceEntityId,
                supportRequestId = notification.SupportRequestId,
                invitationId = notification.InvitationId,
                deliveryState = notification.Status.ToString(),
                recipientCount = 0,
                occurredAt,
                correlationId = notification.CorrelationId
            })));
    }

    private static NotificationEventType GetMessageEventType(PortalRole role) =>
        role is PortalRole.GlobalAdministrator or PortalRole.GlobalSupportUser
            ? NotificationEventType.GlobalSupportReply
            : NotificationEventType.TeamReply;

    private bool IsEnabled => runtimeSettings?.Current.SendGrid.Enabled ?? configuredEnabled == true;
}