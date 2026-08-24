namespace SupportPortal.Domain.Notifications;

public enum NotificationDeliveryState
{
    Pending,
    RetryableFailure,
    Sent,
    PermanentFailure,
    Suppressed
}