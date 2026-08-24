using SupportPortal.Application.Branding;
using SupportPortal.Application.Notifications;

namespace SupportPortal.Application.Tests.Notifications;

public sealed class BrandedEmailRendererTests
{
    [Fact]
    public void RequestActivityContainsOnlyAllowedFieldsAndEncodesMarkup()
    {
        var brand = BrandingResolver.Resolve(
            new BrandingInput("Northwind Support", "NS", null, null, null, null, null, "Support Operations", "support@example.test", "Northwind"),
            "Production");
        var renderer = new BrandedEmailRenderer(brand);

        var content = renderer.RenderRequestActivity(
            "Team reply",
            "SP-000123",
            "<script>alert(1)</script>",
            "Team User",
            "In Progress",
            "https://portal.example/requests/123");

        Assert.Contains("Northwind Support", content.PlainText, StringComparison.Ordinal);
        Assert.Contains("SP-000123", content.PlainText, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", content.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", content.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("full request description", content.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("support@example.test", content.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void InvitationContainsLinkButDoesNotRenderTokenAsSeparateText()
    {
        var brand = BrandingResolver.Resolve(new BrandingInput(null, null, null, null, null, null, null, null, null, null), "Production");
        var renderer = new BrandedEmailRenderer(brand);
        var link = "https://portal.example/invitations/accept?token=ABC123";

        var content = renderer.RenderInvitation(link);

        Assert.Contains(link, content.PlainText, StringComparison.Ordinal);
        Assert.Contains("href=\"https://portal.example/invitations/accept?token=ABC123\"", content.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("Invitation token:", content.PlainText, StringComparison.OrdinalIgnoreCase);
    }
}