using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.Notifications;

public sealed class NotificationDelivery
{
    private NotificationDelivery()
    {
    }

    public NotificationDelivery(
        Guid notificationDeliveryId,
        Guid notificationId,
        NotificationRecipientKind recipientKind,
        Guid? recipientUserId,
        string? recipientAddress,
        string recipientKey,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(recipientKey))
        {
            throw new DomainException("Notification recipient key is required.");
        }

        if (recipientKind == NotificationRecipientKind.PortalUser && recipientUserId is null ||
            recipientKind != NotificationRecipientKind.PortalUser && recipientUserId is not null ||
            recipientKind == NotificationRecipientKind.ConfiguredGlobalMailbox && string.IsNullOrWhiteSpace(recipientAddress) ||
            recipientKind != NotificationRecipientKind.ConfiguredGlobalMailbox && recipientAddress is not null)
        {
            throw new DomainException("Notification recipient details are invalid.");
        }

        NotificationDeliveryId = notificationDeliveryId;
        NotificationId = notificationId;
        RecipientKind = recipientKind;
        RecipientUserId = recipientUserId;
        RecipientAddress = recipientAddress;
        RecipientKey = recipientKey;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        NextAttemptAt = createdAt;
    }

    public Guid NotificationDeliveryId { get; private set; }

    public Guid NotificationId { get; private set; }

    public NotificationRecipientKind RecipientKind { get; private set; }

    public Guid? RecipientUserId { get; private set; }

    public string? RecipientAddress { get; private set; }

    public string RecipientKey { get; private set; } = string.Empty;

    public NotificationDeliveryState State { get; private set; } = NotificationDeliveryState.Pending;

    public int AttemptCount { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public string? LeaseOwner { get; private set; }

    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public int? LastHttpStatus { get; private set; }

    public NotificationFailureCategory LastFailureCategory { get; private set; } = NotificationFailureCategory.None;

    public string? ProviderMessageId { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? PermanentFailedAt { get; private set; }

    public DateTimeOffset? SuppressedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string RowVersion { get; private set; } = "1";

    public bool IsDue(DateTimeOffset now) =>
        State is NotificationDeliveryState.Pending or NotificationDeliveryState.RetryableFailure &&
        (NextAttemptAt is null || NextAttemptAt <= now) &&
        (LeaseExpiresAt is null || LeaseExpiresAt <= now);

    public NotificationAttempt StartAttempt(Guid attemptId, string leaseOwner, DateTimeOffset now, TimeSpan leaseDuration)
        => StartAttempt(attemptId, leaseOwner, leaseOwner, now, leaseDuration);

    public NotificationAttempt StartAttempt(
        Guid attemptId,
        string leaseOwner,
        string correlationId,
        DateTimeOffset now,
        TimeSpan leaseDuration)
    {
        if (!IsDue(now))
        {
            throw new DomainException("Notification delivery is not due.");
        }

        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new DomainException("Notification delivery lease owner is required.");
        }

        AttemptCount++;
        LeaseOwner = leaseOwner;
        LeaseExpiresAt = now.Add(leaseDuration);
        UpdatedAt = now;
        Touch();
        return new NotificationAttempt(attemptId, NotificationDeliveryId, AttemptCount, now, correlationId);
    }

    public bool OwnsLease(string leaseOwner, DateTimeOffset now) =>
        StringComparer.Ordinal.Equals(LeaseOwner, leaseOwner) && LeaseExpiresAt > now;

    public void MarkAccepted(string? providerMessageId, DateTimeOffset at)
    {
        EnsureLeased();
        State = NotificationDeliveryState.Sent;
        ProviderMessageId = providerMessageId;
        SentAt = at;
        LastHttpStatus = 202;
        LastFailureCategory = NotificationFailureCategory.None;
        ClearLease(at);
    }

    public void MarkRetryable(int? statusCode, NotificationFailureCategory category, DateTimeOffset nextAttemptAt, DateTimeOffset at)
    {
        EnsureLeased();
        State = NotificationDeliveryState.RetryableFailure;
        LastHttpStatus = statusCode;
        LastFailureCategory = category;
        NextAttemptAt = nextAttemptAt;
        ClearLease(at);
    }

    public void MarkPermanent(int? statusCode, NotificationFailureCategory category, DateTimeOffset at)
    {
        EnsureLeased();
        State = NotificationDeliveryState.PermanentFailure;
        LastHttpStatus = statusCode;
        LastFailureCategory = category;
        PermanentFailedAt = at;
        NextAttemptAt = null;
        ClearLease(at);
    }

    public void MarkSuppressed(NotificationFailureCategory category, DateTimeOffset at)
    {
        State = NotificationDeliveryState.Suppressed;
        LastFailureCategory = category;
        SuppressedAt = at;
        NextAttemptAt = null;
        ClearLease(at);
    }

    public void RecoverExpiredLease(DateTimeOffset at)
    {
        if (LeaseExpiresAt is null || LeaseExpiresAt > at || State is NotificationDeliveryState.Sent or NotificationDeliveryState.PermanentFailure or NotificationDeliveryState.Suppressed)
        {
            return;
        }

        LeaseOwner = null;
        LeaseExpiresAt = null;
        LastFailureCategory = NotificationFailureCategory.AmbiguousNetwork;
        UpdatedAt = at;
        Touch();
    }

    private void EnsureLeased()
    {
        if (string.IsNullOrWhiteSpace(LeaseOwner))
        {
            throw new DomainException("Notification delivery is not leased.");
        }
    }

    private void ClearLease(DateTimeOffset at)
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
        UpdatedAt = at;
        Touch();
    }

    private void Touch()
    {
        RowVersion = Guid.NewGuid().ToString("N");
    }
}