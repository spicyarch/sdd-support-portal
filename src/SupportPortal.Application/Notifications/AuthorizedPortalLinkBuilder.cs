namespace SupportPortal.Application.Notifications;

public sealed class AuthorizedPortalLinkBuilder
{
    private readonly string publicPortalUrl;

    public AuthorizedPortalLinkBuilder(string publicPortalUrl)
    {
        if (string.IsNullOrWhiteSpace(publicPortalUrl))
        {
            throw new ArgumentException("A public portal URL is required.", nameof(publicPortalUrl));
        }

        this.publicPortalUrl = publicPortalUrl.TrimEnd('/');
    }

    public string CreateRequestLink(Guid requestId) =>
        $"{publicPortalUrl}/requests/{requestId:D}";

    public string CreateInvitationLink(string token) =>
        $"{publicPortalUrl}/invitations/accept?token={Uri.EscapeDataString(token)}";
}