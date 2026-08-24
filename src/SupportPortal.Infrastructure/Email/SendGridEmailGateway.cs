using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using SendGrid;
using SendGrid.Helpers.Mail;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Notifications;
using SupportPortal.Domain.Notifications;
using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Infrastructure.Email;

public sealed class SendGridEmailGateway : IEmailDeliveryGateway, IEmailReadinessGateway
{
    private readonly IServiceProvider services;
    private readonly SendGridOptions options;
    private readonly EmailDeliveryAvailability availability;

    public SendGridEmailGateway(
        IServiceProvider services,
        SendGridOptions options,
        EmailDeliveryAvailability availability)
    {
        this.services = services;
        this.options = options;
        this.availability = availability;
    }

    public async Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken)
    {
        if (!availability.CanSend)
        {
            return new EmailDeliveryResult(
                EmailDeliveryOutcome.PermanentFailure,
                null,
                null,
                NotificationFailureCategory.InvalidConfiguration.ToString());
        }

        return await SendCoreAsync(request, cancellationToken);
    }

    public async Task<EmailReadinessResult> CheckAsync(
        EmailReadinessRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        if (!options.Enabled)
        {
            return new EmailReadinessResult(
                request.Mode,
                EmailReadinessOutcome.Disabled,
                "Configuration",
                null,
                "Disabled",
                checkedAt,
                correlationId,
                "NoProviderRequestMade",
                []);
        }

        if (availability.State == EmailDeliveryState.InvalidConfiguration)
        {
            return new EmailReadinessResult(
                request.Mode,
                EmailReadinessOutcome.InvalidConfiguration,
                "Configuration",
                null,
                "InvalidConfiguration",
                checkedAt,
                correlationId,
                "NoProviderRequestMade",
                availability.InvalidSettingNames);
        }

        if (request.Mode == EmailReadinessMode.Live &&
            (string.IsNullOrWhiteSpace(request.TestRecipient) || !request.ConfirmLiveSend))
        {
            return new EmailReadinessResult(
                request.Mode,
                EmailReadinessOutcome.ProviderRejected,
                "Configuration",
                null,
                "InvalidConfiguration",
                checkedAt,
                correlationId,
                "NoProviderRequestMade",
                ["TestRecipient", "ConfirmLiveSend"]);
        }

        var recipient = request.Mode == EmailReadinessMode.Sandbox
            ? "readiness@example.invalid"
            : request.TestRecipient!.Trim();
        var delivery = await SendCoreAsync(
            new EmailDeliveryRequest(
                Guid.NewGuid(),
                recipient,
                null,
                options.SenderAddress!,
                options.SenderDisplayName!,
                options.ReplyToAddress,
                $"{options.SenderDisplayName}: SendGrid readiness check",
                "This is a SendGrid readiness check.",
                "<p>This is a SendGrid readiness check.</p>",
                request.Mode == EmailReadinessMode.Sandbox),
            cancellationToken);

        if (request.Mode == EmailReadinessMode.Sandbox && delivery.StatusCode == 200)
        {
            return new EmailReadinessResult(
                request.Mode,
                EmailReadinessOutcome.Ready,
                "PayloadValidation",
                200,
                "None",
                checkedAt,
                correlationId,
                "NoEmailSent",
                []);
        }

        if (request.Mode == EmailReadinessMode.Live && delivery.Outcome == EmailDeliveryOutcome.Accepted)
        {
            return new EmailReadinessResult(
                request.Mode,
                EmailReadinessOutcome.Accepted,
                "SenderAcceptance",
                delivery.StatusCode,
                "None",
                checkedAt,
                correlationId,
                "AcceptedBySendGridMailboxDeliveryUnconfirmed",
                []);
        }

        var outcome = delivery.Outcome == EmailDeliveryOutcome.RetryableFailure
            ? EmailReadinessOutcome.ProviderUnavailable
            : EmailReadinessOutcome.ProviderRejected;
        return new EmailReadinessResult(
            request.Mode,
            outcome,
            request.Mode == EmailReadinessMode.Sandbox ? "PayloadValidation" : "SenderAcceptance",
            delivery.StatusCode,
            delivery.FailureCategory ?? "ProviderFailure",
            checkedAt,
            correlationId,
            "NoEmailSent",
            []);
    }

    private async Task<EmailDeliveryResult> SendCoreAsync(EmailDeliveryRequest request, CancellationToken cancellationToken)
    {
        var message = new SendGridMessage();
        var senderAddress = string.IsNullOrWhiteSpace(request.SenderAddress)
            ? options.SenderAddress
            : request.SenderAddress;
        var senderDisplayName = string.IsNullOrWhiteSpace(request.SenderDisplayName)
            ? options.SenderDisplayName
            : request.SenderDisplayName;
        message.SetFrom(new EmailAddress(
            senderAddress!,
            senderDisplayName));
        message.AddTo(new EmailAddress(request.RecipientAddress, request.RecipientDisplayName));
        message.SetGlobalSubject(request.Subject);
        message.AddContent(MimeType.Text, request.PlainTextContent);
        message.AddContent(MimeType.Html, request.HtmlContent);
        var replyTo = request.ReplyToAddress ?? options.ReplyToAddress;
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            message.SetReplyTo(new EmailAddress(replyTo));
        }

        message.AddGlobalCustomArg("notification_id", request.NotificationId.ToString("N"));
        message.SetClickTracking(false, false);
        message.SetOpenTracking(false);
        message.SetSubscriptionTracking(false);
        if (request.SandboxMode)
        {
            message.SetSandBoxMode(true);
        }

        try
        {
            var response = await services.GetRequiredService<ISendGridClient>().SendEmailAsync(message, cancellationToken);
            var providerMessageId = response.Headers.TryGetValues("X-Message-Id", out var messageIds)
                ? messageIds.FirstOrDefault()
                : null;
            var statusCode = (int)response.StatusCode;
            if (statusCode is 200 or 202)
            {
                return new EmailDeliveryResult(EmailDeliveryOutcome.Accepted, statusCode, providerMessageId, null);
            }

            var retryable = SendGridFailureClassifier.IsRetryable(statusCode);
            return new EmailDeliveryResult(
                retryable ? EmailDeliveryOutcome.RetryableFailure : EmailDeliveryOutcome.PermanentFailure,
                statusCode,
                null,
                SendGridFailureClassifier.Classify(statusCode),
                retryable ? GetRetryAfter(response.Headers) : null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new EmailDeliveryResult(EmailDeliveryOutcome.RetryableFailure, 408, null, "Timeout", Ambiguous: true);
        }
        catch (HttpRequestException)
        {
            return new EmailDeliveryResult(EmailDeliveryOutcome.RetryableFailure, null, null, "AmbiguousNetwork", Ambiguous: true);
        }
        catch (Exception)
        {
            return new EmailDeliveryResult(EmailDeliveryOutcome.RetryableFailure, null, null, "Unknown", Ambiguous: true);
        }
    }

    private static TimeSpan? GetRetryAfter(HttpResponseHeaders headers)
    {
        if (headers.RetryAfter?.Delta is TimeSpan delta && delta >= TimeSpan.Zero)
        {
            return delta;
        }

        if (headers.RetryAfter?.Date is DateTimeOffset date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay >= TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        if (headers.TryGetValues("X-RateLimit-Reset", out var resetValues) &&
            long.TryParse(resetValues.FirstOrDefault(), out var unixSeconds))
        {
            var delay = DateTimeOffset.FromUnixTimeSeconds(unixSeconds) - DateTimeOffset.UtcNow;
            return delay >= TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }
}