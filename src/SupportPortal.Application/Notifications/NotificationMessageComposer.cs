using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Notifications;

namespace SupportPortal.Application.Notifications;

public sealed class NotificationMessageComposer
{
    private readonly IPortalStore store;
    private readonly BrandedEmailRenderer renderer;
    private readonly AuthorizedPortalLinkBuilder links;
    private readonly IInvitationTokenService invitationTokens;

    public NotificationMessageComposer(
        IPortalStore store,
        EffectiveBrandProfile brand,
        string publicPortalUrl,
        IInvitationTokenService invitationTokens)
    {
        this.store = store;
        renderer = new BrandedEmailRenderer(brand);
        links = new AuthorizedPortalLinkBuilder(publicPortalUrl);
        this.invitationTokens = invitationTokens;
    }

    public EmailDeliveryRequest Compose(Notification notification, NotificationRecipientCandidate recipient, bool sandboxMode = false)
    {
        var (recipientAddress, recipientDisplayName, content) = notification.EventType == NotificationEventType.InvitationCreated
            ? ComposeInvitation(notification)
            : ComposeRequestActivity(notification);
        if (recipient.Kind == NotificationRecipientKind.ConfiguredGlobalMailbox)
        {
            recipientAddress = recipient.Address ?? throw new InvalidOperationException("Configured notification address is missing.");
        }
        else if (recipient.Kind == NotificationRecipientKind.PortalUser)
        {
            var user = recipient.UserId is Guid userId ? store.GetUser(userId) : null;
            recipientAddress = user?.Email ?? throw new InvalidOperationException("Notification recipient address is unavailable.");
            recipientDisplayName = user.DisplayName;
        }

        return new EmailDeliveryRequest(
            notification.NotificationId,
            recipientAddress,
            recipientDisplayName,
            string.Empty,
            string.Empty,
            null,
            content.Subject,
            content.PlainText,
            content.Html,
            sandboxMode);
    }

    private (string Address, string? DisplayName, BrandedEmailContent Content) ComposeRequestActivity(Notification notification)
    {
        if (notification.SupportRequestId is not Guid requestId || store.GetRequest(requestId) is not { } request)
        {
            throw new InvalidOperationException("Notification support request is unavailable.");
        }

        var author = store.GetUser(notification.ActorUserId)?.DisplayName ?? "Portal user";
        var content = renderer.RenderRequestActivity(
            GetEventLabel(notification.EventType),
            request.Reference,
            request.Subject,
            author,
            request.Status.ToString(),
            links.CreateRequestLink(request.SupportRequestId));
        return (string.Empty, null, content);
    }

    private (string Address, string? DisplayName, BrandedEmailContent Content) ComposeInvitation(Notification notification)
    {
        if (notification.InvitationId is not Guid invitationId || store.GetInvitation(invitationId) is not { } invitation)
        {
            throw new InvalidOperationException("Notification invitation is unavailable.");
        }

        var token = invitationTokens.CreateToken(invitation.InvitationId);
        var content = renderer.RenderInvitation(links.CreateInvitationLink(token));
        return (invitation.Email, null, content);
    }

    private static string GetEventLabel(NotificationEventType eventType) => eventType switch
    {
        NotificationEventType.RequestCreated => "Request created",
        NotificationEventType.TeamReply => "Team reply",
        NotificationEventType.GlobalSupportReply => "Global support reply",
        _ => eventType.ToString()
    };
}