using SupportPortal.Application.Branding;
using SupportPortal.Application.Settings;
using SupportPortal.Contracts.Settings;
using SupportPortal.Domain.Settings;

namespace SupportPortal.Application.Tests.Settings;

public sealed class SettingsCandidateValidatorTests
{
    [Fact]
    public void ProductionRejectsLoopbackImageAndPortalUrls()
    {
        var request = ValidRequest() with
        {
            Branding = new BrandingSettingsUpdate(
                null,
                null,
                "http://localhost/logo.png",
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            SendGrid = ValidRequest().SendGrid with { PublicPortalUrl = "http://localhost:5258" }
        };

        var result = new SettingsCandidateValidator("Production").Validate(request, Baseline());

        Assert.Contains("Branding:LogoUrl", result.InvalidSettingNames);
        Assert.Contains("SendGrid:PublicPortalUrl", result.InvalidSettingNames);
    }

    [Fact]
    public void DevelopmentAcceptsLoopbackUrlsAndNormalizesRecipients()
    {
        var request = ValidRequest() with
        {
            Branding = new BrandingSettingsUpdate(
                null,
                null,
                "http://localhost/logo.png",
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            SendGrid = ValidRequest().SendGrid with
            {
                PublicPortalUrl = "http://localhost:5258",
                GlobalSupportRecipients = ["Support@example.test", "support@example.test"]
            }
        };

        var result = new SettingsCandidateValidator("Development").Validate(request, Baseline());

        Assert.DoesNotContain("Branding:LogoUrl", result.InvalidSettingNames);
        Assert.Contains("SendGrid:GlobalSupportRecipients", result.InvalidSettingNames);
    }

    [Fact]
    public void SendGridNumericBoundariesAndOrderingAreValidated()
    {
        var request = ValidRequest() with
        {
            SendGrid = ValidRequest().SendGrid with
            {
                HttpTimeoutSeconds = 0,
                MaximumAttempts = 11,
                MinimumBackoffSeconds = 0,
                MaximumBackoffSeconds = -1,
                BatchSize = 101,
                LeaseSeconds = 15
            }
        };

        var result = new SettingsCandidateValidator("Production").Validate(request, Baseline());

        Assert.Contains("SendGrid:HttpTimeoutSeconds", result.InvalidSettingNames);
        Assert.Contains("SendGrid:MaximumAttempts", result.InvalidSettingNames);
        Assert.Contains("SendGrid:MinimumBackoffSeconds", result.InvalidSettingNames);
        Assert.Contains("SendGrid:MaximumBackoffSeconds", result.InvalidSettingNames);
        Assert.Contains("SendGrid:BatchSize", result.InvalidSettingNames);
        Assert.Contains("SendGrid:LeaseSeconds", result.InvalidSettingNames);
    }

    [Fact]
    public void EnabledSendGridRequiresACompleteProfileAndConfiguredKey()
    {
        var request = ValidRequest() with
        {
            SendGrid = new SendGridSettingsUpdate(
                true,
                " ",
                "invalid-email",
                "",
                [],
                "https://portal.example.test",
                15,
                4,
                5,
                60,
                "Global",
                25,
                60)
        };

        var result = new SettingsCandidateValidator("Production").Validate(request, Baseline(apiKeyConfigured: false));

        Assert.Contains("SendGrid:ApiKey", result.InvalidSettingNames);
        Assert.Contains("SendGrid:SenderDisplayName", result.InvalidSettingNames);
        Assert.Contains("SendGrid:SenderAddress", result.InvalidSettingNames);
        Assert.Contains("SendGrid:ReplyToAddress", result.InvalidSettingNames);
        Assert.Contains("SendGrid:GlobalSupportRecipients", result.InvalidSettingNames);
    }

    [Fact]
    public void UnspecifiedValuesUseTheEffectiveBaseline()
    {
        var result = new SettingsCandidateValidator("Production").Validate(
            ValidRequest() with
            {
                Branding = new BrandingSettingsUpdate("Changed", null, null, null, null, null, null, null, null, null),
                Invitation = new InvitationSettingsUpdate(null, null),
                SendGrid = ValidRequest().SendGrid with { Enabled = false }
            },
            Baseline(apiKeyConfigured: true));

        Assert.True(result.IsValid);
        Assert.Equal("Changed", result.Branding.ProductName);
        Assert.Equal("https://portal.example.test/invitations/accept", result.InvitationAcceptanceBaseUrl);
        Assert.Equal(72, result.InvitationLifetimeHours);
        Assert.Equal("sender@example.test", result.SendGrid.SenderAddress);
        Assert.Equal(SettingsApiKeyMode.Inherit, result.ApiKeyMode);
    }

    private static SettingsValidationBaseline Baseline(bool apiKeyConfigured = true) => new(
        new BrandingInput(null, null, null, null, null, null, null, null, null, null),
        "https://portal.example.test/invitations/accept",
        72,
        new SendGridSettingsBaseline(
            false,
            "Support Portal",
            "sender@example.test",
            "support@example.test",
            ["support@example.test"],
            "https://portal.example.test",
            15,
            4,
            5,
            60,
            "Global",
            25,
            60),
        SettingsApiKeyMode.Inherit,
        apiKeyConfigured);

    private static UpdateGlobalSettingsRequest ValidRequest() => new(
        new BrandingSettingsUpdate(null, null, null, null, null, null, null, null, null, null),
        new InvitationSettingsUpdate(null, null),
        new SendGridSettingsUpdate(
            false,
            "Support Portal",
            "sender@example.test",
            "support@example.test",
            ["support@example.test"],
            "https://portal.example.test",
            15,
            4,
            5,
            60,
            "Global",
            25,
            60));
}
