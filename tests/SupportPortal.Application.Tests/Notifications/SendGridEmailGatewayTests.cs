using System.Net;
using System.Net.Http.Headers;
using SendGrid;
using SendGrid.Helpers.Mail;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Notifications;
using SupportPortal.Application.Settings;
using SupportPortal.Domain.Settings;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;

namespace SupportPortal.Application.Tests.Notifications;

public sealed class SendGridEmailGatewayTests
{
    [Fact]
    public async Task AcceptedResponseMapsToSentAndBuildsPrivateMessage()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        httpResponse.Headers.Add("X-Message-Id", "safe-provider-id");
        var fakeClient = new FakeSendGridClient(new Response(HttpStatusCode.Accepted, new StringContent(string.Empty), httpResponse.Headers));
        using var provider = new ServiceCollection().AddSingleton<ISendGridClient>(fakeClient).BuildServiceProvider();
        var options = ValidOptions();
        var gateway = new SendGridEmailGateway(
            provider,
            options,
            new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], DateTimeOffset.UtcNow));

        var result = await gateway.SendAsync(
            new EmailDeliveryRequest(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "recipient@example.test",
                "Recipient",
                string.Empty,
                string.Empty,
                string.Empty,
                "Request created",
                "Allowed content",
                "<p>Allowed content</p>"),
            CancellationToken.None);

        Assert.Equal(EmailDeliveryOutcome.Accepted, result.Outcome);
        Assert.Equal(202, result.StatusCode);
        Assert.Equal("safe-provider-id", result.ProviderMessageId);
        Assert.NotNull(fakeClient.Message);
        var serialized = fakeClient.Message!.Serialize();
        Assert.Contains("recipient@example.test", serialized, StringComparison.Ordinal);
        Assert.Contains("notification_id", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"cc\"", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"bcc\"", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sensitive", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetryableAndPermanentStatusesAreClassifiedWithoutReadingProviderBody()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var fakeClient = new FakeSendGridClient(new Response(HttpStatusCode.TooManyRequests, new StringContent("recipient@example.test"), httpResponse.Headers));
        using var provider = new ServiceCollection().AddSingleton<ISendGridClient>(fakeClient).BuildServiceProvider();
        var gateway = new SendGridEmailGateway(
            provider,
            ValidOptions(),
            new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], DateTimeOffset.UtcNow));

        var result = await gateway.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(EmailDeliveryOutcome.RetryableFailure, result.Outcome);
        Assert.Equal("RateLimited", result.FailureCategory);
        Assert.Null(result.ProviderMessageId);
    }

    [Fact]
    public async Task UnexpectedProviderTransportExceptionIsRetryableAndRedacted()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<ISendGridClient>(new FakeSendGridClient(exception: new InvalidOperationException("contains provider details")))
            .BuildServiceProvider();
        var gateway = new SendGridEmailGateway(
            provider,
            ValidOptions(),
            new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], DateTimeOffset.UtcNow));

        var result = await gateway.SendAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(EmailDeliveryOutcome.RetryableFailure, result.Outcome);
        Assert.Equal("Unknown", result.FailureCategory);
        Assert.True(result.Ambiguous);
        Assert.DoesNotContain("provider details", result.FailureCategory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CurrentRuntimeSnapshotControlsReadinessOverStartupOptions()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new EffectiveSettingsSnapshot(
            "saved-revision",
            SettingsSource.AdministratorOverride,
            new EffectiveBrandProfile(
                "Support Portal",
                "SP",
                "SP",
                null,
                null,
                "#135E96",
                "#006B54",
                "#006B54",
                "Support Operations",
                "support@example.test",
                null,
                "saved-revision"),
            "https://portal.example.test/invitations/accept",
            72,
            new EffectiveSendGridSettings(
                false,
                null,
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
            new RuntimeEmailAvailability(RuntimeEmailAvailabilityState.Disabled, [], now),
            false,
            SettingsApiKeyMode.Cleared,
            now);
        var state = new RuntimeSettingsState(snapshot);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var gateway = new SendGridEmailGateway(
            provider,
            ValidOptions(),
            new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], now),
            state);

        var result = await gateway.CheckAsync(
            new EmailReadinessRequest(EmailReadinessMode.Sandbox, null, false),
            "correlation",
            CancellationToken.None);

        Assert.Equal(EmailReadinessOutcome.Disabled, result.Outcome);
        Assert.Equal("NoProviderRequestMade", result.DeliveryMeaning);
    }

    [Fact]
    public async Task DisabledReadinessDoesNotCallTheProvider()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var fakeClient = new FakeSendGridClient(new Response(HttpStatusCode.OK, new StringContent(string.Empty), httpResponse.Headers));
        using var provider = new ServiceCollection().AddSingleton<ISendGridClient>(fakeClient).BuildServiceProvider();
        var options = ValidOptions();
        options.Enabled = false;
        var gateway = new SendGridEmailGateway(
            provider,
            options,
            new EmailDeliveryAvailability(EmailDeliveryState.Disabled, [], DateTimeOffset.UtcNow));

        var result = await gateway.CheckAsync(
            new EmailReadinessRequest(EmailReadinessMode.Sandbox, null, false),
            "correlation",
            CancellationToken.None);

        Assert.Equal(EmailReadinessOutcome.Disabled, result.Outcome);
        Assert.Equal("NoProviderRequestMade", result.DeliveryMeaning);
        Assert.Equal(0, fakeClient.SendEmailCalls);
    }

    [Fact]
    public async Task InvalidConfigurationDoesNotCallTheProvider()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var fakeClient = new FakeSendGridClient(new Response(HttpStatusCode.OK, new StringContent(string.Empty), httpResponse.Headers));
        using var provider = new ServiceCollection().AddSingleton<ISendGridClient>(fakeClient).BuildServiceProvider();
        var gateway = new SendGridEmailGateway(
            provider,
            ValidOptions(),
            new EmailDeliveryAvailability(EmailDeliveryState.InvalidConfiguration, ["SendGrid:ApiKey"], DateTimeOffset.UtcNow));

        var result = await gateway.CheckAsync(
            new EmailReadinessRequest(EmailReadinessMode.Sandbox, null, false),
            "correlation",
            CancellationToken.None);

        Assert.Equal(EmailReadinessOutcome.InvalidConfiguration, result.Outcome);
        Assert.Equal("NoProviderRequestMade", result.DeliveryMeaning);
        Assert.Equal(["SendGrid:ApiKey"], result.InvalidSettingNames);
        Assert.Equal(0, fakeClient.SendEmailCalls);
    }

    [Fact]
    public async Task Sandbox200MeansReadyWithoutEmailDelivery()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var fakeClient = new FakeSendGridClient(new Response(HttpStatusCode.OK, new StringContent("provider-body-secret"), httpResponse.Headers));
        using var provider = new ServiceCollection().AddSingleton<ISendGridClient>(fakeClient).BuildServiceProvider();
        var gateway = new SendGridEmailGateway(
            provider,
            ValidOptions(),
            new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], DateTimeOffset.UtcNow));

        var result = await gateway.CheckAsync(
            new EmailReadinessRequest(EmailReadinessMode.Sandbox, null, false),
            "correlation",
            CancellationToken.None);

        Assert.Equal(EmailReadinessOutcome.Ready, result.Outcome);
        Assert.Equal(200, result.ProviderHttpStatus);
        Assert.Equal("NoEmailSent", result.DeliveryMeaning);
        Assert.DoesNotContain("provider-body-secret", result.FailureCategory, StringComparison.Ordinal);
        Assert.Equal(1, fakeClient.SendEmailCalls);
    }

    [Fact]
    public async Task Live202MeansAcceptedButMailboxDeliveryIsUnconfirmed()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.Accepted);
        var fakeClient = new FakeSendGridClient(new Response(HttpStatusCode.Accepted, new StringContent(string.Empty), httpResponse.Headers));
        using var provider = new ServiceCollection().AddSingleton<ISendGridClient>(fakeClient).BuildServiceProvider();
        var gateway = new SendGridEmailGateway(
            provider,
            ValidOptions(),
            new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], DateTimeOffset.UtcNow));

        var result = await gateway.CheckAsync(
            new EmailReadinessRequest(EmailReadinessMode.Live, "operator@example.test", true),
            "correlation",
            CancellationToken.None);

        Assert.Equal(EmailReadinessOutcome.Accepted, result.Outcome);
        Assert.Equal(202, result.ProviderHttpStatus);
        Assert.Equal("AcceptedBySendGridMailboxDeliveryUnconfirmed", result.DeliveryMeaning);
        Assert.Equal(1, fakeClient.SendEmailCalls);
    }

    [Theory]
    [InlineData(400, EmailReadinessOutcome.ProviderRejected, "RequestRejected")]
    [InlineData(503, EmailReadinessOutcome.ProviderUnavailable, "ProviderFailure")]
    public async Task ProviderStatusesMapToSafeReadinessOutcomes(int statusCode, EmailReadinessOutcome expectedOutcome, string expectedCategory)
    {
        using var httpResponse = new HttpResponseMessage((HttpStatusCode)statusCode);
        var fakeClient = new FakeSendGridClient(new Response((HttpStatusCode)statusCode, new StringContent("provider-body-secret"), httpResponse.Headers));
        using var provider = new ServiceCollection().AddSingleton<ISendGridClient>(fakeClient).BuildServiceProvider();
        var gateway = new SendGridEmailGateway(
            provider,
            ValidOptions(),
            new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], DateTimeOffset.UtcNow));

        var result = await gateway.CheckAsync(
            new EmailReadinessRequest(EmailReadinessMode.Live, "operator@example.test", true),
            "correlation",
            CancellationToken.None);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(statusCode, result.ProviderHttpStatus);
        Assert.Equal(expectedCategory, result.FailureCategory);
        Assert.Equal("NoEmailSent", result.DeliveryMeaning);
        Assert.Equal(1, fakeClient.SendEmailCalls);
    }

    [Fact]
    public async Task NetworkFailureMapsToProviderUnavailableWithoutProviderBody()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<ISendGridClient>(new FakeSendGridClient(exception: new HttpRequestException("provider-body-secret")))
            .BuildServiceProvider();
        var gateway = new SendGridEmailGateway(
            provider,
            ValidOptions(),
            new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], DateTimeOffset.UtcNow));

        var result = await gateway.CheckAsync(
            new EmailReadinessRequest(EmailReadinessMode.Sandbox, null, false),
            "correlation",
            CancellationToken.None);

        Assert.Equal(EmailReadinessOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal("NetworkUnavailable", result.FailureCategory);
        Assert.DoesNotContain("provider-body-secret", result.FailureCategory, StringComparison.Ordinal);
    }

    private static EmailDeliveryRequest CreateRequest() => new(
        Guid.NewGuid(),
        "recipient@example.test",
        null,
        "",
        "",
        "",
        "Subject",
        "Plain",
        "<p>Plain</p>");

    private static SendGridOptions ValidOptions() => new()
    {
        Enabled = true,
        ApiKey = "not-a-real-key",
        SenderDisplayName = "Support Portal",
        SenderAddress = "sender@example.test",
        ReplyToAddress = "reply@example.test",
        GlobalSupportRecipients = ["support@example.test"],
        PublicPortalUrl = "https://portal.example.test",
        DataResidency = "Global",
        HttpTimeoutSeconds = 15,
        MaximumAttempts = 4,
        MinimumBackoffSeconds = 1,
        MaximumBackoffSeconds = 10,
        LeaseSeconds = 60,
        BatchSize = 25
    };

    private sealed class FakeSendGridClient(Response? response = null, Exception? exception = null) : ISendGridClient
    {
        private readonly Response? response = response;
        private readonly Exception? exception = exception;

        public SendGridMessage? Message { get; private set; }

        public int SendEmailCalls { get; private set; }

        public string UrlPath { get; set; } = string.Empty;

        public string Version { get; set; } = "v3";

        public string MediaType { get; set; } = "application/json";

        public AuthenticationHeaderValue AddAuthorization(KeyValuePair<string, string> header) =>
            new(header.Key, header.Value);

        public Task<Response> MakeRequest(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
            Task.FromResult(response!);

        public Task<Response> RequestAsync(SendGridClient.Method method, string? requestBody = null, string? queryParams = null, string? urlPath = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(response!);

        public Task<Response> SendEmailAsync(SendGridMessage message, CancellationToken cancellationToken = default)
        {
            SendEmailCalls++;
            if (exception is not null)
            {
                throw exception;
            }

            Message = message;
            return Task.FromResult(response!);
        }
    }
}