using SupportPortal.Application;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Notifications;
using SupportPortal.Contracts.Requests;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Notifications;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Application.Tests.Notifications;

public sealed class NotificationSchedulingTests
{
    [Fact]
    public void AcceptedRequestAndRetryCreateOneLogicalNotification()
    {
        var store = new InMemoryPortalStore();
        var scheduler = new NotificationScheduler(store, enabled: true);
        var service = new SupportPortalService(store, TimeProvider.System, notificationScheduler: scheduler);
        var principal = Principal("team-user-a");
        var key = Guid.NewGuid();
        var input = new CreateSupportRequestRequest("Notify once", "Normal", "Do not include this description in email.");

        var first = service.CreateRequest(principal, key, input);
        var retry = service.CreateRequest(principal, key, input);

        var notifications = store.GetNotifications();
        Assert.Equal(first.RequestId, retry.RequestId);
        var notification = Assert.Single(notifications);
        Assert.Equal(NotificationEventType.RequestCreated, notification.EventType);
        Assert.Equal(first.RequestId, notification.SourceEntityId);
        Assert.Single(store.GetAuditEvents(), item => item.EventType == "NotificationScheduled");
    }

    [Fact]
    public async Task ProcessorSendsOnePrivateBrandedRequestNotificationWithoutDescription()
    {
        var store = new InMemoryPortalStore();
        var scheduler = new NotificationScheduler(store, enabled: true);
        var service = new SupportPortalService(store, TimeProvider.System, notificationScheduler: scheduler);
        var request = service.CreateRequest(
            Principal("team-user-a"),
            Guid.NewGuid(),
            new CreateSupportRequestRequest("Private activity", "Normal", "Sensitive request description"));
        var brand = BrandingResolver.Resolve(new BrandingInput("Northwind Support", "NS", null, null, null, null, null, "Operations", "support@example.test", null), "Development");
        var gateway = new FakeEmailDeliveryGateway();
        var processor = new NotificationDeliveryProcessor(
            store,
            gateway,
            new NotificationRecipientPlanner(store, ["global-support@example.test"]),
            new NotificationMessageComposer(store, brand, "http://localhost:5258", new FakeInvitationTokenService()),
            new NotificationRetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), new Random(1)),
            TimeProvider.System,
            TimeSpan.FromMinutes(1),
            enabled: true,
            canSend: true,
            batchSize: 25);

        var processed = await processor.ProcessOnceAsync();

        Assert.Equal(1, processed);
        var sent = Assert.Single(gateway.Requests);
        Assert.Equal("global-support@example.test", sent.RecipientAddress);
        Assert.Contains(request.Reference, sent.PlainTextContent, StringComparison.Ordinal);
        Assert.Contains(request.Subject, sent.PlainTextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive request description", sent.PlainTextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive request description", sent.HtmlContent, StringComparison.Ordinal);
        var notification = Assert.Single(store.GetNotifications());
        Assert.Equal(NotificationStatus.Completed, notification.Status);
        Assert.Equal(NotificationDeliveryState.Sent, Assert.Single(store.GetNotificationDeliveries(notification.NotificationId)).State);
    }

    [Fact]
    public void GlobalReplyExcludesAuthorAndKeepsTeamParticipantsEligible()
    {
        var store = new InMemoryPortalStore();
        var scheduler = new NotificationScheduler(store, enabled: true);
        var service = new SupportPortalService(store, TimeProvider.System, notificationScheduler: scheduler);
        var request = service.CreateRequest(Principal("team-user-a"), Guid.NewGuid(), new CreateSupportRequestRequest("Reply recipients", "Normal", "Description"));
        service.PostMessage(Principal("team-user-a"), request.RequestId, Guid.NewGuid(), new CreateMessageRequest("Team context", Guid.NewGuid()));
        var globalReply = service.PostMessage(Principal("global-support"), request.RequestId, Guid.NewGuid(), new CreateMessageRequest("Support context", Guid.NewGuid()));
        var notification = store.GetNotification(NotificationEventType.GlobalSupportReply, globalReply.MessageId);

        var planner = new NotificationRecipientPlanner(store, []);
        var candidates = planner.PlanEligible(notification!, DateTimeOffset.UtcNow);

        var recipient = Assert.Single(candidates);
        Assert.Equal(DevelopmentIdentities.All.Single(item => item.Key == "team-user-a").UserId, recipient.UserId);
        Assert.DoesNotContain(candidates, item => item.UserId == DevelopmentIdentities.All.Single(identity => identity.Key == "global-support").UserId);
    }

    private static PortalPrincipal Principal(string key)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Role, identity.TeamId, true);
    }

    private sealed class FakeEmailDeliveryGateway : SupportPortal.Application.Abstractions.IEmailDeliveryGateway
    {
        public List<EmailDeliveryRequest> Requests { get; } = [];

        public Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new EmailDeliveryResult(EmailDeliveryOutcome.Accepted, 202, "provider-message-id", null));
        }
    }

    private sealed class FakeInvitationTokenService : IInvitationTokenService
    {
        public TimeSpan Lifetime => TimeSpan.FromHours(72);

        public string CreateToken(Guid invitationId) => invitationId.ToString("N");

        public string HashToken(string token) => token;

        public string CreateAcceptanceLink(string token) => $"https://portal.example/invitations/accept?token={token}";
    }
}