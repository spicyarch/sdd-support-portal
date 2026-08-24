using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Application.Tests.Notifications;

public sealed class SendGridOptionsValidatorTests
{
    [Fact]
    public void DisabledConfigurationIsAvailableWithoutAKey()
    {
        var result = new SendGridOptionsValidator().Validate(new SendGridOptions { Enabled = false }, "Development", DateTimeOffset.UtcNow);

        Assert.Equal(EmailDeliveryState.Disabled, result.State);
        Assert.Empty(result.InvalidSettingNames);
    }

    [Fact]
    public void EnabledIncompleteConfigurationReportsOnlySettingNames()
    {
        var result = new SendGridOptionsValidator().Validate(new SendGridOptions
        {
            Enabled = true,
            ApiKey = "secret-value"
        }, "Production", DateTimeOffset.UtcNow);

        Assert.Equal(EmailDeliveryState.InvalidConfiguration, result.State);
        Assert.Contains("SendGrid:SenderAddress", result.InvalidSettingNames);
        Assert.Contains("SendGrid:PublicPortalUrl", result.InvalidSettingNames);
        Assert.DoesNotContain("secret-value", string.Join(',', result.InvalidSettingNames), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidProductionConfigurationIsReady()
    {
        var result = new SendGridOptionsValidator().Validate(new SendGridOptions
        {
            Enabled = true,
            ApiKey = "secret-value",
            SenderDisplayName = "Support Portal",
            SenderAddress = "sender@example.com",
            ReplyToAddress = "support@example.com",
            GlobalSupportRecipients = ["global@example.com"],
            PublicPortalUrl = "https://portal.example.com",
            DataResidency = "Global",
            HttpTimeoutSeconds = 15,
            MaximumAttempts = 4,
            MinimumBackoffSeconds = 5,
            MaximumBackoffSeconds = 60,
            BatchSize = 25,
            LeaseSeconds = 60
        }, "Production", DateTimeOffset.UtcNow);

        Assert.Equal(EmailDeliveryState.Ready, result.State);
        Assert.Empty(result.InvalidSettingNames);
    }
}