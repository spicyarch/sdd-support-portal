using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SupportPortal.Application;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Notifications;
using SupportPortal.Contracts.Requests;
using SupportPortal.Domain.Notifications;
using SupportPortal.Infrastructure.Persistence;
using SupportPortal.Api.IntegrationTests.Persistence;

namespace SupportPortal.Api.IntegrationTests.Notifications;

public sealed class NotificationRecoveryIntegrationTests
{
    [SqlFact]
    public async Task SqlLeaseClaimAllowsOnlyOneCompetingWorker()
    {
        using var context = SqlTestSupport.CreateContext();
        var store = new EfPortalStore(context);
        var now = DateTimeOffset.UtcNow;
        var principal = SqlTestSupport.SeedTeamActor(store, now);
        var service = new SupportPortalService(
            store,
            TimeProvider.System,
            notificationScheduler: new NotificationScheduler(store, enabled: true));
        var request = service.CreateRequest(
            principal,
            Guid.NewGuid(),
            new CreateSupportRequestRequest("SQL lease exclusivity", "Normal", "Synthetic description."));
        var notification = store.GetNotification(NotificationEventType.RequestCreated, request.RequestId)!;
        var delivery = new NotificationDelivery(
            Guid.NewGuid(),
            notification.NotificationId,
            NotificationRecipientKind.ConfiguredGlobalMailbox,
            null,
            "lease@example.test",
            "lease-recipient-key",
            now);
        store.Execute(() =>
        {
            store.AddNotificationDelivery(delivery);
            notification.MarkRecipientsExpanded(1, now);
        });

        var connectionString = context.Database.GetDbConnection().ConnectionString;
        var firstTask = Task.Run(() => TryClaim(connectionString, delivery.NotificationDeliveryId, "worker-a", now));
        var secondTask = Task.Run(() => TryClaim(connectionString, delivery.NotificationDeliveryId, "worker-b", now));
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Single(results, result => result is not null);
        Assert.Single(results, result => result is null);
    }

    [SqlFact]
    public async Task SqlExpiredLeaseIsReclaimedAsAmbiguousBeforeARecoverySend()
    {
        using var context = SqlTestSupport.CreateContext();
        var store = new EfPortalStore(context);
        var now = DateTimeOffset.UtcNow;
        var principal = SqlTestSupport.SeedTeamActor(store, now);
        var service = new SupportPortalService(
            store,
            TimeProvider.System,
            notificationScheduler: new NotificationScheduler(store, enabled: true));
        var request = service.CreateRequest(
            principal,
            Guid.NewGuid(),
            new CreateSupportRequestRequest("SQL expired lease", "Normal", "Synthetic description."));
        var notification = store.GetNotification(NotificationEventType.RequestCreated, request.RequestId)!;
        const string address = "recovery@example.test";
        var delivery = new NotificationDelivery(
            Guid.NewGuid(),
            notification.NotificationId,
            NotificationRecipientKind.ConfiguredGlobalMailbox,
            null,
            address,
            RecipientKey(address),
            now);
        store.Execute(() =>
        {
            store.AddNotificationDelivery(delivery);
            notification.MarkRecipientsExpanded(1, now);
        });

        var abandoned = store.TryStartNotificationAttempt(
            delivery.NotificationDeliveryId,
            "abandoned-worker",
            now,
            TimeSpan.FromSeconds(1));
        Assert.NotNull(abandoned);

        var clock = new MutableTimeProvider(now.AddSeconds(2));
        var gateway = new RecordingGateway();
        var brand = BrandingResolver.Resolve(
            new BrandingInput("Support Portal", "SP", null, null, null, null, null, "Support", "support@example.test", null),
            "Development");
        var processor = new NotificationDeliveryProcessor(
            store,
            gateway,
            new NotificationRecipientPlanner(store, [address]),
            new NotificationMessageComposer(store, brand, "http://localhost:5258", new TestInvitationTokenService()),
            new NotificationRetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), new Random(1)),
            clock,
            TimeSpan.FromSeconds(1),
            enabled: true,
            canSend: true,
            batchSize: 25);

        await processor.ProcessOnceAsync();

        var attempts = store.GetNotificationAttempts(delivery.NotificationDeliveryId);
        Assert.Equal(2, attempts.Count);
        Assert.Equal(NotificationAttemptOutcome.AmbiguousFailure, attempts[0].Outcome);
        Assert.Equal(NotificationAttemptOutcome.Accepted, attempts[1].Outcome);
        Assert.Equal(NotificationDeliveryState.Sent, store.GetNotificationDelivery(delivery.NotificationDeliveryId)!.State);
        Assert.Single(gateway.Requests);
    }

    private static (Notification Notification, NotificationDelivery Delivery, NotificationAttempt Attempt)? TryClaim(
        string connectionString,
        Guid deliveryId,
        string owner,
        DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<SupportPortalDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        using var context = new SupportPortalDbContext(options);
        return new EfPortalStore(context).TryStartNotificationAttempt(deliveryId, owner, now, TimeSpan.FromMinutes(1));
    }

    private static string RecipientKey(string address) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"mailbox:{address.ToUpperInvariant()}")));

    private sealed class RecordingGateway : IEmailDeliveryGateway
    {
        public List<EmailDeliveryRequest> Requests { get; } = [];

        public Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new EmailDeliveryResult(EmailDeliveryOutcome.Accepted, 202, "provider-id", null));
        }
    }

    private sealed class TestInvitationTokenService : IInvitationTokenService
    {
        public TimeSpan Lifetime => TimeSpan.FromHours(72);

        public string CreateToken(Guid invitationId) => invitationId.ToString("N");

        public string HashToken(string token) => token;

        public string CreateAcceptanceLink(string token) => $"https://portal.example/invitations/accept?token={token}";
    }

    private sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
    }
}
