using System.Security.Cryptography;
using System.Text;
using SupportPortal.Application;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Notifications;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Contracts.Requests;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Notifications;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Application.Tests.Notifications;

public sealed class NotificationDeliveryLifecycleTests
{
    [Fact]
    public async Task InvitationDeliveryReconstructsTokenWithoutStoringAddressOrLink()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-24T00:00:00Z"));
        var store = new InMemoryPortalStore();
        var tokens = new DeterministicInvitationTokenService();
        var service = new SupportPortalService(
            store,
            clock,
            tokens,
            new NotificationScheduler(store, enabled: true));
        var invitation = service.CreateInvitation(
            Principal("global-admin"),
            Guid.NewGuid(),
            new CreateInvitationRequest("new-user@example.test", "TeamUser", DevelopmentIdentities.TeamAId));
        var gateway = new FakeEmailDeliveryGateway();
        var processor = CreateProcessor(store, clock, gateway, tokens);

        await processor.ProcessOnceAsync();

        var notification = Assert.Single(store.GetNotifications());
        var delivery = Assert.Single(store.GetNotificationDeliveries(notification.NotificationId));
        Assert.Equal(NotificationRecipientKind.InvitationRecipient, delivery.RecipientKind);
        Assert.Null(delivery.RecipientAddress);
        Assert.Contains($"TOKEN-{invitation.InvitationId:N}", gateway.Requests.Single().PlainTextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("new-user@example.test", string.Join('|', store.GetAuditEvents().Select(item => item.Metadata)), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(NotificationDeliveryState.Sent, delivery.State);
    }

    [Fact]
    public async Task AcceptedInvitationIsSuppressedBeforeProviderCall()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-24T00:00:00Z"));
        var store = new InMemoryPortalStore();
        var tokens = new DeterministicInvitationTokenService();
        var service = new SupportPortalService(store, clock, tokens, new NotificationScheduler(store, enabled: true));
        service.CreateInvitation(
            Principal("global-admin"),
            Guid.NewGuid(),
            new CreateInvitationRequest("accepted@example.test", "TeamUser", DevelopmentIdentities.TeamAId));
        var invitation = Assert.Single(store.GetInvitations());
        invitation.Accept(clock.GetUtcNow());
        var gateway = new FakeEmailDeliveryGateway();

        await CreateProcessor(store, clock, gateway, tokens).ProcessOnceAsync();

        Assert.Empty(gateway.Requests);
        Assert.Equal(NotificationStatus.Suppressed, Assert.Single(store.GetNotifications()).Status);
    }

    [Fact]
    public async Task RetryAndExpiredLeaseRecoveryDoNotCreateAnotherLogicalNotification()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-24T00:00:00Z"));
        var store = new InMemoryPortalStore();
        var tokens = new DeterministicInvitationTokenService();
        var service = new SupportPortalService(store, clock, tokens, new NotificationScheduler(store, enabled: true));
        var request = service.CreateRequest(Principal("team-user-a"), Guid.NewGuid(), new CreateSupportRequestRequest("Lifecycle", "Normal", "Description"));
        var notification = Assert.Single(store.GetNotifications());
        var planner = new NotificationRecipientPlanner(store, ["global-support@example.test"]);
        var candidate = Assert.Single(planner.PlanEligible(notification, clock.GetUtcNow()));
        store.Execute(() =>
        {
            store.AddNotificationDelivery(new NotificationDelivery(
                Guid.NewGuid(),
                notification.NotificationId,
                candidate.Kind,
                candidate.UserId,
                candidate.Address,
                candidate.RecipientKey,
                clock.GetUtcNow()));
            notification.MarkRecipientsExpanded(1, clock.GetUtcNow());
        });

        var abandoned = store.TryStartNotificationAttempt(
            Assert.Single(store.GetNotificationDeliveries(notification.NotificationId)).NotificationDeliveryId,
            "abandoned-worker",
            clock.GetUtcNow(),
            TimeSpan.FromSeconds(1));
        Assert.NotNull(abandoned);
        clock.Advance(TimeSpan.FromSeconds(2));
        var gateway = new FakeEmailDeliveryGateway(
            new EmailDeliveryResult(EmailDeliveryOutcome.Accepted, 202, "recovered", null));

        await CreateProcessor(store, clock, gateway, tokens).ProcessOnceAsync();

        Assert.Single(store.GetNotifications());
        Assert.Equal(NotificationDeliveryState.Sent, Assert.Single(store.GetNotificationDeliveries(notification.NotificationId)).State);
        var attempts = store.GetNotificationAttempts(Assert.Single(store.GetNotificationDeliveries(notification.NotificationId)).NotificationDeliveryId);
        Assert.Equal(2, attempts.Count);
        Assert.Equal(NotificationAttemptOutcome.AmbiguousFailure, attempts[0].Outcome);
        Assert.Single(gateway.Requests);
        Assert.Equal(request.RequestId, notification.SupportRequestId);
    }

    private static NotificationDeliveryProcessor CreateProcessor(
        InMemoryPortalStore store,
        MutableTimeProvider clock,
        FakeEmailDeliveryGateway gateway,
        IInvitationTokenService tokens)
    {
        var brand = BrandingResolver.Resolve(new BrandingInput("Support Portal", "SP", null, null, null, null, null, "Support", "support@example.test", null), "Development");
        return new NotificationDeliveryProcessor(
            store,
            gateway,
            new NotificationRecipientPlanner(store, ["global-support@example.test"]),
            new NotificationMessageComposer(store, brand, "http://localhost:5258", tokens),
            new NotificationRetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), new Random(1)),
            clock,
            TimeSpan.FromSeconds(1),
            enabled: true,
            canSend: true,
            batchSize: 25);
    }

    private static PortalPrincipal Principal(string key)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Role, identity.TeamId, true);
    }

    private sealed class FakeEmailDeliveryGateway(params EmailDeliveryResult[] results) : IEmailDeliveryGateway
    {
        private readonly Queue<EmailDeliveryResult> results = new(results.Length == 0
            ? [new EmailDeliveryResult(EmailDeliveryOutcome.Accepted, 202, "accepted", null)]
            : results);

        public List<EmailDeliveryRequest> Requests { get; } = [];

        public Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(results.Count == 1 ? results.Peek() : results.Dequeue());
        }
    }

    private sealed class DeterministicInvitationTokenService : IInvitationTokenService
    {
        public TimeSpan Lifetime => TimeSpan.FromHours(72);

        public string CreateToken(Guid invitationId) => $"TOKEN-{invitationId:N}";

        public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        public string CreateAcceptanceLink(string token) => $"https://portal.example/invitations/accept?token={token}";
    }

    private sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public DateTimeOffset Current { get; private set; } = current;

        public override DateTimeOffset GetUtcNow() => Current;

        public void Advance(TimeSpan duration) => Current = Current.Add(duration);
    }
}