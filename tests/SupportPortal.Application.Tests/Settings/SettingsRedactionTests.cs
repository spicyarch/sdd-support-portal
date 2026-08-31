using SupportPortal.Application.Settings;
using SupportPortal.Contracts.Operations;
using SupportPortal.Contracts.Settings;
using SupportPortal.Application.Notifications;

namespace SupportPortal.Application.Tests.Settings;

public sealed class SettingsRedactionTests
{
    [Fact]
    public void SettingsAuditMetadataContainsNamesButNoSensitiveValues()
    {
        const string secret = "super-secret-sendgrid-key";
        const string recipient = "recipient@example.test";
        var metadata = SettingsAuditPolicy.CreateSettingsMetadata(
            SettingsAuditPolicy.SettingsSaved,
            "Succeeded",
            "revision-1",
            ["SendGrid:ApiKey", "SendGrid:GlobalSupportRecipients", recipient, secret],
            "correlation");

        Assert.Contains("SendGrid:ApiKey", metadata, StringComparison.Ordinal);
        Assert.Contains("SendGrid:GlobalSupportRecipients", metadata, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, metadata, StringComparison.Ordinal);
        Assert.DoesNotContain(recipient, metadata, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsFingerprintTracksPresenceWithoutStoringTheSecret()
    {
        var first = Request("first-secret");
        var second = Request("second-secret");

        var firstFingerprint = SettingsAuditPolicy.CreateSettingsFingerprint(first);
        var secondFingerprint = SettingsAuditPolicy.CreateSettingsFingerprint(second);

        Assert.Equal(firstFingerprint, secondFingerprint);
        Assert.DoesNotContain("first-secret", firstFingerprint, StringComparison.Ordinal);
        Assert.DoesNotContain("second-secret", secondFingerprint, StringComparison.Ordinal);
    }

    private static UpdateGlobalSettingsRequest Request(string? apiKey) => new(
        new BrandingSettingsUpdate(null, null, null, null, null, null, null, null, null, null),
        new InvitationSettingsUpdate(null, null),
        new SendGridSettingsUpdate(
            false,
            null,
            null,
            null,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            apiKey));
}
