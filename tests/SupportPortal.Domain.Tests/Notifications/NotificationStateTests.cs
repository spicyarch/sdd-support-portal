using SupportPortal.Domain.Common;
using SupportPortal.Domain.Notifications;

namespace SupportPortal.Domain.Tests.Notifications;

public sealed class NotificationStateTests
{
    [Fact]
    public void RequestNotificationRequiresRequestContext()
    {
        var exception = Assert.Throws<DomainException>(() => new Notification(
            Guid.NewGuid(),
            NotificationEventType.RequestCreated,
            Guid.NewGuid(),
            null,
            null,
            Guid.NewGuid(),
            null,
            DateTimeOffset.UtcNow,
            "correlation"));

        Assert.Equal("Notification source context is invalid.", exception.Message);
    }

    [Fact]
    public void SentDeliveryCompletesNotificationAggregate()
    {
        var now = DateTimeOffset.UtcNow;
        var notification = new Notification(Guid.NewGuid(), NotificationEventType.RequestCreated, Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), null, now, "correlation");
        var delivery = new NotificationDelivery(Guid.NewGuid(), notification.NotificationId, NotificationRecipientKind.ConfiguredGlobalMailbox, null, "support@example.test", "recipient-key", now);
        notification.MarkRecipientsExpanded(1, now);
        var attempt = delivery.StartAttempt(Guid.NewGuid(), "worker", now, TimeSpan.FromMinutes(1));

        delivery.MarkAccepted("provider-id", now.AddSeconds(1));
        attempt.Complete(NotificationAttemptOutcome.Accepted, 202, NotificationFailureCategory.None, "provider-id", now.AddSeconds(1), null, 100);
        notification.Reconcile([delivery]);

        Assert.Equal(NotificationDeliveryState.Sent, delivery.State);
        Assert.Equal(NotificationStatus.Completed, notification.Status);
        Assert.Equal(NotificationAttemptOutcome.Accepted, attempt.Outcome);
    }

    [Fact]
    public void ExpiredLeaseCanBeRecoveredWithoutChangingBusinessContext()
    {
        var now = DateTimeOffset.UtcNow;
        var delivery = new NotificationDelivery(Guid.NewGuid(), Guid.NewGuid(), NotificationRecipientKind.ConfiguredGlobalMailbox, null, "support@example.test", "recipient-key", now);
        var attempt = delivery.StartAttempt(Guid.NewGuid(), "worker", now, TimeSpan.FromSeconds(1));

        delivery.RecoverExpiredLease(now.AddSeconds(2));
        attempt.Complete(NotificationAttemptOutcome.AmbiguousFailure, null, NotificationFailureCategory.AmbiguousNetwork, null, now.AddSeconds(2), now.AddSeconds(2), 2000);

        Assert.Null(delivery.LeaseOwner);
        Assert.Equal(NotificationDeliveryState.Pending, delivery.State);
        Assert.Equal(NotificationAttemptOutcome.AmbiguousFailure, attempt.Outcome);
        Assert.Equal(NotificationFailureCategory.AmbiguousNetwork, delivery.LastFailureCategory);
    }
}