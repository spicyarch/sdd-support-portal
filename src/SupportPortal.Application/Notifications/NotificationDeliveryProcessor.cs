using System.Text.Json;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Notifications;

namespace SupportPortal.Application.Notifications;

public sealed class NotificationDeliveryProcessor
{
    private readonly IPortalStore store;
    private readonly IEmailDeliveryGateway gateway;
    private readonly NotificationRecipientPlanner recipientPlanner;
    private readonly NotificationMessageComposer messageComposer;
    private readonly NotificationRetryPolicy retryPolicy;
    private readonly TimeProvider clock;
    private readonly TimeSpan leaseDuration;
    private readonly bool enabled;
    private readonly bool canSend;
    private readonly int batchSize;

    public NotificationDeliveryProcessor(
        IPortalStore store,
        IEmailDeliveryGateway gateway,
        NotificationRecipientPlanner recipientPlanner,
        NotificationMessageComposer messageComposer,
        NotificationRetryPolicy retryPolicy,
        TimeProvider clock,
        TimeSpan leaseDuration,
        bool enabled,
        bool canSend,
        int batchSize)
    {
        this.store = store;
        this.gateway = gateway;
        this.recipientPlanner = recipientPlanner;
        this.messageComposer = messageComposer;
        this.retryPolicy = retryPolicy;
        this.clock = clock;
        this.leaseDuration = leaseDuration;
        this.enabled = enabled;
        this.canSend = canSend;
        this.batchSize = batchSize;
    }

    public async Task<int> ProcessOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!enabled || !canSend)
        {
            return 0;
        }

        var now = clock.GetUtcNow();
        RecoverExpiredAttempts(now);
        ExpandPendingNotifications(now);
        var processed = 0;
        foreach (var candidate in store.GetDueNotificationDeliveries(now, batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ProcessDeliveryAsync(candidate.NotificationDeliveryId, cancellationToken))
            {
                processed++;
            }
        }

        return processed;
    }

    private void ExpandPendingNotifications(DateTimeOffset now)
    {
        foreach (var notification in store.GetNotifications().Where(item => item.Status == NotificationStatus.PendingRecipients))
        {
            var candidates = recipientPlanner.PlanEligible(notification, now);
            store.Execute(() =>
            {
                var current = store.GetNotification(notification.NotificationId);
                if (current is null || current.Status != NotificationStatus.PendingRecipients)
                {
                    return;
                }

                foreach (var candidate in candidates)
                {
                    if (store.GetNotificationDeliveries(current.NotificationId).Any(item => item.RecipientKey == candidate.RecipientKey))
                    {
                        continue;
                    }

                    store.AddNotificationDelivery(new NotificationDelivery(
                        Guid.NewGuid(),
                        current.NotificationId,
                        candidate.Kind,
                        candidate.UserId,
                        candidate.Kind == NotificationRecipientKind.ConfiguredGlobalMailbox ? candidate.Address : null,
                        candidate.RecipientKey,
                        now));
                }

                current.MarkRecipientsExpanded(candidates.Count, now);
            });
        }
    }

    private async Task<bool> ProcessDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var owner = Guid.NewGuid().ToString("N");
        var lease = TryStartAttempt(deliveryId, owner, now);
        if (lease is null)
        {
            return false;
        }

        var (notification, delivery, attempt) = lease.Value;
        var recipient = recipientPlanner
            .PlanEligible(notification, now)
            .SingleOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.RecipientKey, delivery.RecipientKey));
        if (recipient is null)
        {
            CompleteSuppressed(deliveryId, attempt.NotificationAttemptId, owner, now);
            return true;
        }

        EmailDeliveryResult result;
        try
        {
            result = await gateway.SendAsync(messageComposer.Compose(notification, recipient), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            result = new EmailDeliveryResult(
                EmailDeliveryOutcome.RetryableFailure,
                null,
                null,
                NotificationFailureCategory.Unknown.ToString(),
                Ambiguous: true);
        }

        CompleteAttempt(deliveryId, attempt.NotificationAttemptId, owner, result);
        return true;
    }

    private (Notification Notification, NotificationDelivery Delivery, NotificationAttempt Attempt)? TryStartAttempt(
        Guid deliveryId,
        string owner,
        DateTimeOffset now)
    {
        return store.TryStartNotificationAttempt(deliveryId, owner, now, leaseDuration);
    }

    private void RecoverExpiredAttempts(DateTimeOffset now)
    {
        foreach (var notification in store.GetNotifications())
        {
            foreach (var delivery in store.GetNotificationDeliveries(notification.NotificationId))
            {
                if (delivery.LeaseExpiresAt is not DateTimeOffset expiresAt || expiresAt > now || string.IsNullOrWhiteSpace(delivery.LeaseOwner))
                {
                    continue;
                }

                var attempt = store.GetNotificationAttempts(delivery.NotificationDeliveryId)
                    .LastOrDefault(item => item.CompletedAt is null);
                var owner = delivery.LeaseOwner;
                store.Execute(() =>
                {
                    var current = store.GetNotificationDelivery(delivery.NotificationDeliveryId);
                    if (current is null || !StringComparer.Ordinal.Equals(current.LeaseOwner, owner) || current.LeaseExpiresAt > now)
                    {
                        return;
                    }

                    current.RecoverExpiredLease(now);
                    if (attempt is not null)
                    {
                        attempt.Complete(
                            NotificationAttemptOutcome.AmbiguousFailure,
                            null,
                            NotificationFailureCategory.AmbiguousNetwork,
                            null,
                            now,
                            now,
                            (long)(now - attempt.StartedAt).TotalMilliseconds);
                    }
                });
            }
        }
    }

    private void CompleteSuppressed(Guid deliveryId, Guid attemptId, string owner, DateTimeOffset now)
    {
        store.Execute(() =>
        {
            var delivery = store.GetNotificationDelivery(deliveryId);
            if (delivery is null || !StringComparer.Ordinal.Equals(delivery.LeaseOwner, owner))
            {
                return;
            }

            delivery.MarkSuppressed(NotificationFailureCategory.Suppressed, now);
            var attempt = store.GetNotificationAttempts(deliveryId).Single(item => item.NotificationAttemptId == attemptId);
            attempt.Complete(
                NotificationAttemptOutcome.PermanentFailure,
                null,
                NotificationFailureCategory.Suppressed,
                null,
                now,
                null,
                (long)(now - attempt.StartedAt).TotalMilliseconds);
            Reconcile(delivery.NotificationId);
        });
    }

    private void CompleteAttempt(Guid deliveryId, Guid attemptId, string owner, EmailDeliveryResult result)
    {
        store.Execute(() =>
        {
            var now = clock.GetUtcNow();
            var delivery = store.GetNotificationDelivery(deliveryId);
            if (delivery is null || !StringComparer.Ordinal.Equals(delivery.LeaseOwner, owner))
            {
                return;
            }

            var attempt = store.GetNotificationAttempts(deliveryId).Single(item => item.NotificationAttemptId == attemptId);
            var category = ParseFailureCategory(result.FailureCategory);
            if (result.Outcome == EmailDeliveryOutcome.Accepted)
            {
                delivery.MarkAccepted(result.ProviderMessageId, now);
                attempt.Complete(
                    NotificationAttemptOutcome.Accepted,
                    result.StatusCode,
                    NotificationFailureCategory.None,
                    result.ProviderMessageId,
                    now,
                    null,
                    (long)(now - attempt.StartedAt).TotalMilliseconds);
            }
            else if (result.Outcome == EmailDeliveryOutcome.RetryableFailure && retryPolicy.HasAttemptsRemaining(delivery.AttemptCount))
            {
                var nextAttemptAt = retryPolicy.NextAttemptAt(now, delivery.AttemptCount, result.RetryAfter);
                delivery.MarkRetryable(result.StatusCode, category, nextAttemptAt, now);
                attempt.Complete(
                    result.Ambiguous ? NotificationAttemptOutcome.AmbiguousFailure : NotificationAttemptOutcome.RetryableFailure,
                    result.StatusCode,
                    category,
                    null,
                    now,
                    nextAttemptAt,
                    (long)(now - attempt.StartedAt).TotalMilliseconds);
            }
            else
            {
                delivery.MarkPermanent(result.StatusCode, category, now);
                attempt.Complete(
                    NotificationAttemptOutcome.PermanentFailure,
                    result.StatusCode,
                    category,
                    null,
                    now,
                    null,
                    (long)(now - attempt.StartedAt).TotalMilliseconds);
                var notification = store.GetNotification(delivery.NotificationId);
                store.AddAuditEvent(new AuditEvent(
                    Guid.NewGuid(),
                    now,
                    "NotificationDeliveryFailed",
                    null,
                    "NotificationDelivery",
                    delivery.NotificationDeliveryId,
                    false,
                    JsonSerializer.Serialize(new
                    {
                        notificationId = delivery.NotificationId,
                        notificationDeliveryId = delivery.NotificationDeliveryId,
                        sourceEventType = notification?.EventType.ToString(),
                        sourceEntityId = notification?.SourceEntityId,
                        supportRequestId = notification?.SupportRequestId,
                        invitationId = notification?.InvitationId,
                        deliveryState = delivery.State.ToString(),
                        attemptCount = delivery.AttemptCount,
                        failureCategory = category.ToString(),
                        occurredAt = now,
                        correlationId = attempt.CorrelationId
                    })));
            }

            Reconcile(delivery.NotificationId);
        });
    }

    private void Reconcile(Guid notificationId)
    {
        var notification = store.GetNotification(notificationId);
        if (notification is not null)
        {
            notification.Reconcile(store.GetNotificationDeliveries(notificationId).ToArray());
        }
    }

    private static NotificationFailureCategory ParseFailureCategory(string? category) =>
        Enum.TryParse<NotificationFailureCategory>(category, ignoreCase: true, out var parsed)
            ? parsed
            : NotificationFailureCategory.Unknown;
}