using Microsoft.EntityFrameworkCore;
using SupportPortal.Application;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Notifications;
using SupportPortal.Contracts.Requests;
using SupportPortal.Domain.Notifications;
using SupportPortal.Infrastructure.Persistence;
using SupportPortal.Api.IntegrationTests.Persistence;

namespace SupportPortal.Api.IntegrationTests.Notifications;

public sealed class NotificationSchedulingIntegrationTests
{
    [SqlFact]
    public void SqlSchedulingAndCommandReplayCreateOneLogicalNotification()
    {
        using var context = SqlTestSupport.CreateContext();
        var store = new EfPortalStore(context);
        var now = DateTimeOffset.UtcNow;
        var principal = SqlTestSupport.SeedTeamActor(store, now);
        var service = new SupportPortalService(
            store,
            TimeProvider.System,
            notificationScheduler: new NotificationScheduler(store, enabled: true));
        var key = Guid.NewGuid();
        var input = new CreateSupportRequestRequest("SQL notification request", "Normal", "Synthetic SQL integration description.");

        var first = service.CreateRequest(principal, key, input);
        var replay = service.CreateRequest(principal, key, input);

        Assert.Equal(first.RequestId, replay.RequestId);
        Assert.Equal(1, context.Notifications.Count(item => item.SourceEntityId == first.RequestId));
        Assert.Equal(1, context.CommandReceipts.Count(item => item.ActorUserId == principal.UserId && item.IdempotencyKey == key));
        Assert.Equal(1, context.SupportRequests.Count(item => item.SupportRequestId == first.RequestId));
    }

    [SqlFact]
    public void SqlUniqueRecipientKeyPreventsDuplicateDeliveryRows()
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
            new CreateSupportRequestRequest("SQL recipient uniqueness", "Normal", "Synthetic description."));
        var notification = store.GetNotification(NotificationEventType.RequestCreated, request.RequestId)!;
        var first = new NotificationDelivery(
            Guid.NewGuid(),
            notification.NotificationId,
            NotificationRecipientKind.ConfiguredGlobalMailbox,
            null,
            "sql-recipient@example.test",
            "duplicate-recipient-key",
            now);
        store.Execute(() =>
        {
            store.AddNotificationDelivery(first);
            notification.MarkRecipientsExpanded(1, now);
        });

        var duplicate = new NotificationDelivery(
            Guid.NewGuid(),
            notification.NotificationId,
            NotificationRecipientKind.ConfiguredGlobalMailbox,
            null,
            "another-address@example.test",
            first.RecipientKey,
            now);

        Assert.Throws<DbUpdateException>(() => store.Execute(() => store.AddNotificationDelivery(duplicate)));
        Assert.Single(store.GetNotificationDeliveries(notification.NotificationId));
    }

    [SqlFact]
    public async Task SqlDeliveryFailureDoesNotRollbackTheAcceptedRequest()
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
            new CreateSupportRequestRequest("SQL failure isolation", "Normal", "Sensitive data stays in the request."));
        var gateway = new PermanentFailureGateway();
        var brand = SupportPortal.Application.Branding.BrandingResolver.Resolve(
            new SupportPortal.Application.Branding.BrandingInput("Support Portal", "SP", null, null, null, null, null, "Support", "support@example.test", null),
            "Development");
        var processor = new NotificationDeliveryProcessor(
            store,
            gateway,
            new NotificationRecipientPlanner(store, ["sql-support@example.test"]),
            new NotificationMessageComposer(store, brand, "http://localhost:5258", new TestInvitationTokenService()),
            new NotificationRetryPolicy(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), new Random(1)),
            TimeProvider.System,
            TimeSpan.FromMinutes(1),
            enabled: true,
            canSend: true,
            batchSize: 25);

        await processor.ProcessOnceAsync();

        Assert.NotNull(store.GetRequest(request.RequestId));
        Assert.Equal("Sensitive data stays in the request.", store.GetRequest(request.RequestId)!.Description);
        var notification = store.GetNotification(NotificationEventType.RequestCreated, request.RequestId)!;
        Assert.Equal(NotificationStatus.CompletedWithFailure, notification.Status);
        Assert.Equal(NotificationDeliveryState.PermanentFailure, Assert.Single(store.GetNotificationDeliveries(notification.NotificationId)).State);
    }

    private sealed class PermanentFailureGateway : IEmailDeliveryGateway
    {
        public Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new EmailDeliveryResult(EmailDeliveryOutcome.PermanentFailure, 400, null, "RequestRejected"));
    }

    private sealed class TestInvitationTokenService : IInvitationTokenService
    {
        public TimeSpan Lifetime => TimeSpan.FromHours(72);

        public string CreateToken(Guid invitationId) => invitationId.ToString("N");

        public string HashToken(string token) => token;

        public string CreateAcceptanceLink(string token) => $"https://portal.example/invitations/accept?token={token}";
    }
}
