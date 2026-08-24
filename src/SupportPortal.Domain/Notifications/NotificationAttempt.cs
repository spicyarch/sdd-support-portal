namespace SupportPortal.Domain.Notifications;

public sealed class NotificationAttempt
{
    private NotificationAttempt()
    {
    }

    public NotificationAttempt(
        Guid notificationAttemptId,
        Guid notificationDeliveryId,
        int attemptNumber,
        DateTimeOffset startedAt,
        string correlationId)
    {
        NotificationAttemptId = notificationAttemptId;
        NotificationDeliveryId = notificationDeliveryId;
        AttemptNumber = attemptNumber;
        StartedAt = startedAt;
        CorrelationId = correlationId;
    }

    public Guid NotificationAttemptId { get; private set; }

    public Guid NotificationDeliveryId { get; private set; }

    public int AttemptNumber { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public NotificationAttemptOutcome Outcome { get; private set; } = NotificationAttemptOutcome.Started;

    public int? HttpStatus { get; private set; }

    public NotificationFailureCategory FailureCategory { get; private set; } = NotificationFailureCategory.None;

    public DateTimeOffset? RetryNotBefore { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public long? DurationMilliseconds { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public void Complete(
        NotificationAttemptOutcome outcome,
        int? httpStatus,
        NotificationFailureCategory failureCategory,
        string? providerMessageId,
        DateTimeOffset completedAt,
        DateTimeOffset? retryNotBefore,
        long? durationMilliseconds)
    {
        if (CompletedAt is not null)
        {
            return;
        }

        Outcome = outcome;
        HttpStatus = httpStatus;
        FailureCategory = failureCategory;
        ProviderMessageId = providerMessageId;
        CompletedAt = completedAt;
        RetryNotBefore = retryNotBefore;
        DurationMilliseconds = durationMilliseconds;
    }
}