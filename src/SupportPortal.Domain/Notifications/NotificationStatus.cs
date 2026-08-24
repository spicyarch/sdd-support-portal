namespace SupportPortal.Domain.Notifications;

public enum NotificationStatus
{
    PendingRecipients,
    Active,
    Completed,
    CompletedWithFailure,
    Suppressed
}