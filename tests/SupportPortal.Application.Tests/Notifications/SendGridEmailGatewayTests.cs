using System.Net;
using System.Net.Http.Headers;
using SendGrid;
using SendGrid.Helpers.Mail;
using SupportPortal.Application.Notifications;
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
            if (exception is not null)
            {
                throw exception;
            }

            Message = message;
            return Task.FromResult(response!);
        }
    }
}