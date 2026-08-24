using System.Net;
using SupportPortal.Application.Branding;

namespace SupportPortal.Application.Notifications;

public sealed class BrandedEmailRenderer
{
    private readonly EffectiveBrandProfile brand;

    public BrandedEmailRenderer(EffectiveBrandProfile brand)
    {
        this.brand = brand;
    }

    public BrandedEmailContent RenderRequestActivity(
        string eventType,
        string requestReference,
        string requestSubject,
        string authorDisplayName,
        string currentStatus,
        string requestLink)
    {
        var subject = $"{brand.ProductName}: {eventType} {requestReference}";
        var plainText = string.Join(Environment.NewLine, new[]
        {
            brand.ProductName,
            string.IsNullOrWhiteSpace(brand.OrganizationName) ? null : brand.OrganizationName,
            string.Empty,
            eventType,
            $"Request: {requestReference}",
            $"Subject: {requestSubject}",
            $"Author: {authorDisplayName}",
            $"Status: {currentStatus}",
            string.Empty,
            $"Open the request: {requestLink}",
            $"Support: {brand.SupportContactName} <{brand.SupportContactEmail}>"
        }.Where(value => value is not null)!);

        var html = $"<h1>{Encode(brand.ProductName)}</h1>" +
            (string.IsNullOrWhiteSpace(brand.OrganizationName) ? string.Empty : $"<p>{Encode(brand.OrganizationName)}</p>") +
            $"<p><strong>{Encode(eventType)}</strong></p>" +
            "<dl>" +
            $"<dt>Request</dt><dd>{Encode(requestReference)}</dd>" +
            $"<dt>Subject</dt><dd>{Encode(requestSubject)}</dd>" +
            $"<dt>Author</dt><dd>{Encode(authorDisplayName)}</dd>" +
            $"<dt>Status</dt><dd>{Encode(currentStatus)}</dd>" +
            "</dl>" +
            $"<p><a href=\"{Encode(requestLink)}\">Open the request</a></p>" +
            $"<p>Support: {Encode(brand.SupportContactName)} &lt;{Encode(brand.SupportContactEmail)}&gt;</p>";

        return new BrandedEmailContent(subject, plainText, html);
    }

    public BrandedEmailContent RenderInvitation(string invitationLink)
    {
        var subject = $"{brand.ProductName}: Invitation";
        var plainText = string.Join(Environment.NewLine, new[]
        {
            brand.ProductName,
            string.IsNullOrWhiteSpace(brand.OrganizationName) ? null : brand.OrganizationName,
            string.Empty,
            $"You have been invited to access {brand.ProductName}.",
            $"Accept your invitation: {invitationLink}",
            "This link can be used once and expires according to the portal invitation policy.",
            $"Support: {brand.SupportContactName} <{brand.SupportContactEmail}>"
        }.Where(value => value is not null)!);
        var html = $"<h1>{Encode(brand.ProductName)}</h1>" +
            (string.IsNullOrWhiteSpace(brand.OrganizationName) ? string.Empty : $"<p>{Encode(brand.OrganizationName)}</p>") +
            $"<p>You have been invited to access {Encode(brand.ProductName)}.</p>" +
            $"<p><a href=\"{Encode(invitationLink)}\">Accept your invitation</a></p>" +
            "<p>This link can be used once and expires according to the portal invitation policy.</p>" +
            $"<p>Support: {Encode(brand.SupportContactName)} &lt;{Encode(brand.SupportContactEmail)}&gt;</p>";
        return new BrandedEmailContent(subject, plainText, html);
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}