using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using SendGrid;
using SendGrid.Helpers.Mail;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Notifications;
using SupportPortal.Application.Settings;
using SupportPortal.Domain.Notifications;
using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Infrastructure.Email;

public sealed class SendGridEmailGateway : IEmailDeliveryGateway, IEmailReadinessGateway
{
    private readonly IServiceProvider services;
    private readonly SendGridOptions options;
    private readonly EmailDeliveryAvailability availability;
    private readonly RuntimeSettingsState? runtimeSettings;

    public SendGridEmailGateway(
        IServiceProvider services,
        SendGridOptions options,
        EmailDeliveryAvailability availability,
        RuntimeSettingsState? runtimeSettings = null)
    {
        this.services = services;
        this.options = options;
        this.availability = availability;
        this.runtimeSettings = runtimeSettings;
    }

    public async Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken)
    {
        var currentOptions = CurrentOptions;
        if (!CurrentAvailability.CanSend)
        {
            return new EmailDeliveryResult(
                EmailDeliveryOutcome.PermanentFailure,
                null,
                null,
                NotificationFailureCategory.InvalidConfiguration.ToString());
        }

        return await SendCoreAsync(request, currentOptions, cancellationToken);
    }

    public async Task<EmailReadinessResult> CheckAsync(
        EmailReadinessRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var currentOptions = CurrentOptions;
        var currentAvailability = CurrentAvailability;
        var checkedAt = DateTimeOffset.UtcNow;
        if (!currentOptions.Enabled)
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

        if (currentAvailability.State == EmailDeliveryState.InvalidConfiguration)
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
                currentAvailability.InvalidSettingNames);
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
                currentOptions.SenderAddress!,
                currentOptions.SenderDisplayName!,
                currentOptions.ReplyToAddress,
                $"{currentOptions.SenderDisplayName}: SendGrid readiness check",
                "This is a SendGrid readiness check.",
                "<p>This is a SendGrid readiness check.</p>",
                request.Mode == EmailReadinessMode.Sandbox),
            currentOptions,
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
            ToSafeReadinessFailureCategory(delivery.FailureCategory),
            checkedAt,
            correlationId,
            "NoEmailSent",
            []);
    }

    private static string ToSafeReadinessFailureCategory(string? category) => category switch
    {
        "AmbiguousNetwork" => "NetworkUnavailable",
        "Unknown" or null or "" => "ProviderFailure",
        _ => category
    };

    private async Task<EmailDeliveryResult> SendCoreAsync(
        EmailDeliveryRequest request,
        SendGridOptions currentOptions,
        CancellationToken cancellationToken)
    {
        var message = new SendGridMessage();
        var senderAddress = string.IsNullOrWhiteSpace(request.SenderAddress)
            ? currentOptions.SenderAddress
            : request.SenderAddress;
        var senderDisplayName = string.IsNullOrWhiteSpace(request.SenderDisplayName)
            ? currentOptions.SenderDisplayName
            : request.SenderDisplayName;
        message.SetFrom(new EmailAddress(
            senderAddress!,
            senderDisplayName));
        message.AddTo(new EmailAddress(request.RecipientAddress, request.RecipientDisplayName));
        message.SetGlobalSubject(request.Subject);
        message.AddContent(MimeType.Text, request.PlainTextContent);
        message.AddContent(MimeType.Html, request.HtmlContent);
        var replyTo = request.ReplyToAddress ?? currentOptions.ReplyToAddress;
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
            var response = await SendWithCurrentClientAsync(currentOptions, message, cancellationToken);
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

    private SendGridOptions CurrentOptions => runtimeSettings is null
        ? options
        : new SendGridOptions
        {
            Enabled = runtimeSettings.Current.SendGrid.Enabled,
            ApiKey = runtimeSettings.Current.SendGrid.ApiKey,
            SenderDisplayName = runtimeSettings.Current.SendGrid.SenderDisplayName,
            SenderAddress = runtimeSettings.Current.SendGrid.SenderAddress,
            ReplyToAddress = runtimeSettings.Current.SendGrid.ReplyToAddress,
            GlobalSupportRecipients = runtimeSettings.Current.SendGrid.GlobalSupportRecipients,
            PublicPortalUrl = runtimeSettings.Current.SendGrid.PublicPortalUrl,
            HttpTimeoutSeconds = runtimeSettings.Current.SendGrid.HttpTimeoutSeconds,
            MaximumAttempts = runtimeSettings.Current.SendGrid.MaximumAttempts,
            MinimumBackoffSeconds = runtimeSettings.Current.SendGrid.MinimumBackoffSeconds,
            MaximumBackoffSeconds = runtimeSettings.Current.SendGrid.MaximumBackoffSeconds,
            DataResidency = runtimeSettings.Current.SendGrid.DataResidency,
            BatchSize = runtimeSettings.Current.SendGrid.BatchSize,
            LeaseSeconds = runtimeSettings.Current.SendGrid.LeaseSeconds
        };

    private EmailDeliveryAvailability CurrentAvailability => runtimeSettings is null
        ? availability
        : new EmailDeliveryAvailability(
            (EmailDeliveryState)runtimeSettings.Current.EmailAvailability.State,
            runtimeSettings.Current.EmailAvailability.InvalidSettingNames,
            runtimeSettings.Current.EmailAvailability.CheckedAt);

    private async Task<Response> SendWithCurrentClientAsync(
        SendGridOptions currentOptions,
        SendGridMessage message,
        CancellationToken cancellationToken)
    {
        if (runtimeSettings is null)
        {
            return await services.GetRequiredService<ISendGridClient>().SendEmailAsync(message, cancellationToken);
        }

        var clientOptions = new SendGridClientOptions
        {
            ApiKey = currentOptions.ApiKey
        };
        clientOptions.SetDataResidency(
            StringComparer.OrdinalIgnoreCase.Equals(currentOptions.DataResidency, "Eu") ? "eu" : "global");
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(currentOptions.HttpTimeoutSeconds)
        };
        var client = new SendGridClient(httpClient, clientOptions);
        return await client.SendEmailAsync(message, cancellationToken);
    }
}