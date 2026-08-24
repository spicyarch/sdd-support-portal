using System.Security.Cryptography;
using System.Text;
using SupportPortal.Application;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Notifications;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Notifications;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Application.Tests.Notifications;

public sealed class InvitationNotificationTests
{
    [Fact]
    public void IdempotentInvitationReplayCreatesOneInvitationAndOneNotification()
    {
        var store = new InMemoryPortalStore();
        var tokens = new DeterministicInvitationTokenService();
        var service = new SupportPortalService(
            store,
            TimeProvider.System,
            tokens,
            new NotificationScheduler(store, enabled: true));
        var principal = Principal("global-admin");
        var idempotencyKey = Guid.NewGuid();
        var input = new CreateInvitationRequest("invitee@example.test", "TeamUser", DevelopmentIdentities.TeamAId);

        var first = service.CreateInvitation(principal, idempotencyKey, input);
        var replay = service.CreateInvitation(principal, idempotencyKey, input);

        Assert.Equal(first, replay);
        Assert.Single(store.GetInvitations());
        var notification = Assert.Single(store.GetNotifications());
        Assert.Equal(NotificationEventType.InvitationCreated, notification.EventType);
        Assert.Equal(first.InvitationId, notification.InvitationId);
        Assert.Single(store.GetAuditEvents(), item => item.EventType == "NotificationScheduled");
    }

    [Fact]
    public async Task RevokedAndExpiredInvitationsAreSuppressedBeforeProviderCalls()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-24T00:00:00Z"));
        var store = new InMemoryPortalStore();
        var tokens = new DeterministicInvitationTokenService();
        var service = new SupportPortalService(
            store,
            clock,
            tokens,
            new NotificationScheduler(store, enabled: true));
        service.CreateInvitation(
            Principal("global-admin"),
            Guid.NewGuid(),
            new CreateInvitationRequest("expired@example.test", "TeamUser", DevelopmentIdentities.TeamAId));
        service.CreateInvitation(
            Principal("global-admin"),
            Guid.NewGuid(),
            new CreateInvitationRequest("revoked@example.test", "TeamUser", DevelopmentIdentities.TeamAId));

        var invitations = store.GetInvitations();
        invitations.Single(item => item.Email == "expired@example.test").Expire(clock.GetUtcNow().AddHours(72));
        invitations.Single(item => item.Email == "revoked@example.test").Revoke(DevelopmentIdentities.All[0].UserId);
        var gateway = new RecordingGateway();
        var processor = new NotificationDeliveryProcessor(
            store,
            gateway,
            new NotificationRecipientPlanner(store, []),
            new NotificationMessageComposer(
                store,
                SupportPortal.Application.Branding.BrandingResolver.Resolve(
                    new SupportPortal.Application.Branding.BrandingInput("Support Portal", "SP", null, null, null, null, null, "Support", "support@example.test", null),
                    "Development"),
                "http://localhost:5258",
                tokens),
            new NotificationRetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), new Random(1)),
            clock,
            TimeSpan.FromMinutes(1),
            enabled: true,
            canSend: true,
            batchSize: 25);

        await processor.ProcessOnceAsync();

        Assert.Empty(gateway.Requests);
        Assert.All(store.GetNotifications(), notification => Assert.Equal(NotificationStatus.Suppressed, notification.Status));
        Assert.All(
            store.GetNotifications().SelectMany(notification => store.GetNotificationDeliveries(notification.NotificationId)),
            delivery => Assert.Equal(NotificationDeliveryState.Suppressed, delivery.State));
    }

    [Fact]
    public void DisabledDeliveryPreservesInvitationCreationWithoutScheduling()
    {
        var store = new InMemoryPortalStore();
        var tokens = new DeterministicInvitationTokenService();
        var service = new SupportPortalService(store, TimeProvider.System, tokens, new NotificationScheduler(store, enabled: false));

        var response = service.CreateInvitation(
            Principal("global-admin"),
            Guid.NewGuid(),
            new CreateInvitationRequest("disabled@example.test", "TeamUser", DevelopmentIdentities.TeamAId));

        Assert.Equal("Pending", response.State);
        Assert.Empty(store.GetNotifications());
        Assert.NotNull(response.AcceptanceLink);
    }

    private static PortalPrincipal Principal(string key)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Role, identity.TeamId, true);
    }

    private sealed class RecordingGateway : IEmailDeliveryGateway
    {
        public List<EmailDeliveryRequest> Requests { get; } = [];

        public Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new EmailDeliveryResult(EmailDeliveryOutcome.Accepted, 202, "provider-id", null));
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
        public override DateTimeOffset GetUtcNow() => current;
    }
}
