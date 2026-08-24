using System.Diagnostics;
using SupportPortal.Application;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Notifications;
using SupportPortal.Contracts.Requests;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Notifications;
using SupportPortal.Domain.Teams;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Performance;

public sealed class NotificationPerformanceTests
{
    [Fact]
    public async Task ProcessingHonorsConfiguredBatchSize()
    {
        var store = new InMemoryPortalStore(seed: false);
        store.AddTeam(new Team(DevelopmentIdentities.TeamAId, "Team A", DateTimeOffset.UtcNow));
        var service = new SupportPortalService(store, TimeProvider.System, notificationScheduler: new NotificationScheduler(store, enabled: true));
        var principal = TeamPrincipal();
        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < 100; index++)
        {
            service.CreateRequest(
                principal,
                Guid.NewGuid(),
                new CreateSupportRequestRequest($"Request {index:000}", "Normal", "Synthetic description"));
        }
        stopwatch.Stop();

        var gateway = new RecordingGateway();
        var processor = CreateProcessor(store, TimeProvider.System, gateway, batchSize: 25);

        var processed = await processor.ProcessOnceAsync();

        Assert.Equal(25, processed);
        Assert.Equal(25, gateway.Requests.Count);
        Assert.Equal(75, store.GetNotificationDeliveriesByState(NotificationDeliveryState.Pending));
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PendingRetryableWorkSurvivesProcessorRecreation()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-24T00:00:00Z"));
        var store = new InMemoryPortalStore(seed: false);
        store.AddTeam(new Team(DevelopmentIdentities.TeamAId, "Team A", clock.GetUtcNow()));
        var service = new SupportPortalService(store, clock, notificationScheduler: new NotificationScheduler(store, enabled: true));
        service.CreateRequest(
            TeamPrincipal(),
            Guid.NewGuid(),
            new CreateSupportRequestRequest("Recoverable request", "Normal", "Synthetic description"));
        var firstGateway = new RecordingGateway(new EmailDeliveryResult(EmailDeliveryOutcome.RetryableFailure, 503, null, "ProviderFailure"));

        var firstProcessor = CreateProcessor(store, clock, firstGateway, batchSize: 25);
        await firstProcessor.ProcessOnceAsync();
        Assert.Equal(1, store.GetNotificationDeliveriesByState(NotificationDeliveryState.RetryableFailure));

        clock.Advance(TimeSpan.FromMinutes(1));
        var secondGateway = new RecordingGateway(new EmailDeliveryResult(EmailDeliveryOutcome.Accepted, 202, "provider-id", null));
        var secondProcessor = CreateProcessor(store, clock, secondGateway, batchSize: 25);

        await secondProcessor.ProcessOnceAsync();

        Assert.Single(secondGateway.Requests);
        Assert.Equal(1, store.GetNotificationDeliveriesByState(NotificationDeliveryState.Sent));
        Assert.Single(store.GetNotifications());
        Assert.Equal(NotificationStatus.Completed, Assert.Single(store.GetNotifications()).Status);
    }

    private static NotificationDeliveryProcessor CreateProcessor(
        InMemoryPortalStore store,
        TimeProvider clock,
        RecordingGateway gateway,
        int batchSize)
    {
        var brand = BrandingResolver.Resolve(
            new BrandingInput("Support Portal", "SP", null, null, null, null, null, "Support", "support@example.test", null),
            "Development");
        return new NotificationDeliveryProcessor(
            store,
            gateway,
            new NotificationRecipientPlanner(store, ["global-support@example.test"]),
            new NotificationMessageComposer(store, brand, "http://localhost:5258", new TestInvitationTokenService()),
            new NotificationRetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), new Random(1)),
            clock,
            TimeSpan.FromSeconds(30),
            enabled: true,
            canSend: true,
            batchSize);
    }

    private static PortalPrincipal TeamPrincipal() =>
        new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DevelopmentIdentities.TenantId,
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Team User",
            PortalRole.TeamUser,
            DevelopmentIdentities.TeamAId,
            true);

    private sealed class RecordingGateway(params EmailDeliveryResult[] results) : IEmailDeliveryGateway
    {
        private readonly Queue<EmailDeliveryResult> results = new(results.Length == 0
            ? [new EmailDeliveryResult(EmailDeliveryOutcome.Accepted, 202, "provider-id", null)]
            : results);

        public List<EmailDeliveryRequest> Requests { get; } = [];

        public Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(results.Count == 1 ? results.Peek() : results.Dequeue());
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
        public DateTimeOffset Current { get; private set; } = current;

        public override DateTimeOffset GetUtcNow() => Current;

        public void Advance(TimeSpan amount) => Current = Current.Add(amount);
    }
}
