using SupportPortal.Application.Branding;
using SupportPortal.Application.Commands;
using SupportPortal.Application.Settings;
using SupportPortal.Contracts.Settings;
using SupportPortal.Domain.Settings;

namespace SupportPortal.Application.Tests.Settings;

public sealed class SettingsValidationTests
{
    [Fact]
    public void InvalidCandidateReportsOnlySafeSettingNames()
    {
        var request = ValidRequest(enabled: false) with
        {
            Branding = new BrandingSettingsUpdate(
                new string('x', 101),
                null,
                "http://remote.example/logo.png",
                null,
                "#FFFFFF",
                "not-a-color",
                null,
                null,
                "invalid-email",
                null),
            Invitation = new InvitationSettingsUpdate("https://portal.example.test/invitations/accept?unsafe=true", 0),
            SendGrid = ValidRequest(enabled: false).SendGrid with
            {
                GlobalSupportRecipients = ["duplicate@example.test", "DUPLICATE@example.test"],
                HttpTimeoutSeconds = 0,
                DataResidency = "Unknown"
            }
        };

        var result = new SettingsCandidateValidator("Production").Validate(request, Baseline(apiKeyConfigured: false));

        Assert.Contains("Branding:ProductName", result.InvalidSettingNames);
        Assert.Contains("Branding:LogoUrl", result.InvalidSettingNames);
        Assert.Contains("Branding:PrimaryColor", result.InvalidSettingNames);
        Assert.Contains("Branding:AccentColor", result.InvalidSettingNames);
        Assert.Contains("Branding:SupportContactEmail", result.InvalidSettingNames);
        Assert.Contains("Portal:InvitationAcceptanceBaseUrl", result.InvalidSettingNames);
        Assert.Contains("Portal:InvitationLifetimeHours", result.InvalidSettingNames);
        Assert.Contains("SendGrid:GlobalSupportRecipients", result.InvalidSettingNames);
        Assert.Contains("SendGrid:HttpTimeoutSeconds", result.InvalidSettingNames);
        Assert.Contains("SendGrid:DataResidency", result.InvalidSettingNames);
        Assert.DoesNotContain("invalid-email", string.Join(',', result.InvalidSettingNames), StringComparison.Ordinal);
    }

    [Fact]
    public void BlankApiKeyPreservesAnExistingKey()
    {
        var result = new SettingsCandidateValidator("Production")
            .Validate(ValidRequest(enabled: true), Baseline(apiKeyConfigured: true));

        Assert.True(result.IsValid);
        Assert.Equal(SettingsApiKeyMode.Inherit, result.ApiKeyMode);
        Assert.Null(result.ReplacementApiKey);
    }

    [Fact]
    public void ClearAndReplacementCannotBeCombined()
    {
        var request = ValidRequest(enabled: true) with
        {
            SendGrid = ValidRequest(enabled: true).SendGrid with
            {
                ApiKey = "secret-value",
                ClearApiKey = true
            }
        };

        var result = new SettingsCandidateValidator("Production").Validate(request, Baseline(apiKeyConfigured: true));

        Assert.Contains("SendGrid:ApiKey", result.InvalidSettingNames);
        Assert.DoesNotContain("secret-value", string.Join(',', result.InvalidSettingNames), StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsFingerprintIgnoresSecretValueButTracksSecretAction()
    {
        var first = ValidRequest(enabled: true) with
        {
            SendGrid = ValidRequest(enabled: true).SendGrid with { ApiKey = "first-secret" }
        };
        var second = first with
        {
            SendGrid = first.SendGrid with { ApiKey = "second-secret" }
        };

        var firstFingerprint = IdempotencyService.FingerprintSettings(first);
        var secondFingerprint = IdempotencyService.FingerprintSettings(second);

        Assert.Equal(firstFingerprint, secondFingerprint);
        Assert.DoesNotContain("first-secret", firstFingerprint, StringComparison.Ordinal);
        Assert.DoesNotContain("second-secret", secondFingerprint, StringComparison.Ordinal);
    }

    private static SettingsValidationBaseline Baseline(bool apiKeyConfigured) => new(
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

    private static UpdateGlobalSettingsRequest ValidRequest(bool enabled) => new(
        new BrandingSettingsUpdate(null, null, null, null, null, null, null, null, null, null),
        new InvitationSettingsUpdate(null, null),
        new SendGridSettingsUpdate(
            enabled,
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
