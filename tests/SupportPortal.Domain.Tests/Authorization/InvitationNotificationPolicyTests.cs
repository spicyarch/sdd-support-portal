using SupportPortal.Domain.Common;
using SupportPortal.Domain.Notifications;

namespace SupportPortal.Domain.Tests.Authorization;

public sealed class InvitationNotificationPolicyTests
{
    [Fact]
    public void InvitationNotificationCannotReferenceARequest()
    {
        var exception = Assert.Throws<DomainException>(() => new Notification(
            Guid.NewGuid(),
            NotificationEventType.InvitationCreated,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            DateTimeOffset.UtcNow,
            "correlation"));

        Assert.Equal("Notification source context is invalid.", exception.Message);
    }

    [Fact]
    public void InvitationRecipientDeliveryDoesNotStoreRecipientAddress()
    {
        var delivery = new NotificationDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NotificationRecipientKind.InvitationRecipient,
            null,
            null,
            "invitation-key",
            DateTimeOffset.UtcNow);

        Assert.Null(delivery.RecipientUserId);
        Assert.Null(delivery.RecipientAddress);
        Assert.Equal(NotificationRecipientKind.InvitationRecipient, delivery.RecipientKind);
    }
}
