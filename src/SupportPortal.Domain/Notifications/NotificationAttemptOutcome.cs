namespace SupportPortal.Domain.Notifications;

public enum NotificationAttemptOutcome
{
    Started,
    Accepted,
    RetryableFailure,
    PermanentFailure,
    AmbiguousFailure
}