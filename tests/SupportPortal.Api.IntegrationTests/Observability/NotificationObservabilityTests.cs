using System.Text.Json;
using SupportPortal.Application;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Notifications;
using SupportPortal.Contracts.Requests;
using SupportPortal.Domain.Notifications;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Observability;

public sealed class NotificationObservabilityTests
{
    [Fact]
    public async Task PermanentFailureAuditContainsOnlyTheApprovedMetadataAllowlist()
    {
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(
            store,
            TimeProvider.System,
            notificationScheduler: new NotificationScheduler(store, enabled: true));
        var request = service.CreateRequest(
            Principal("team-user-a"),
            Guid.NewGuid(),
            new CreateSupportRequestRequest("Canary subject", "Normal", "CANARY-DESCRIPTION-MUST-NOT-APPEAR"));
        var notification = store.GetNotification(NotificationEventType.RequestCreated, request.RequestId)!;
        var processor = new NotificationDeliveryProcessor(
            store,
            new PermanentFailureGateway(),
            new NotificationRecipientPlanner(store, ["canary-recipient@example.test"]),
            new NotificationMessageComposer(
                store,
                BrandingResolver.Resolve(new BrandingInput("Support Portal", "SP", null, null, null, null, null, "Support", "support@example.test", null), "Development"),
                "http://localhost:5258",
                new TestInvitationTokenService()),
            new NotificationRetryPolicy(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), new Random(1)),
            TimeProvider.System,
            TimeSpan.FromMinutes(1),
            enabled: true,
            canSend: true,
            batchSize: 25);

        await processor.ProcessOnceAsync();

        var failure = Assert.Single(store.GetAuditEvents(), item => item.EventType == "NotificationDeliveryFailed");
        using var metadata = JsonDocument.Parse(failure.Metadata!);
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "notificationId",
            "notificationDeliveryId",
            "sourceEventType",
            "sourceEntityId",
            "supportRequestId",
            "invitationId",
            "deliveryState",
            "attemptCount",
            "failureCategory",
            "occurredAt",
            "correlationId"
        };
        Assert.All(metadata.RootElement.EnumerateObject(), property => Assert.Contains(property.Name, allowed));
        Assert.DoesNotContain("canary-recipient@example.test", failure.Metadata!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CANARY-DESCRIPTION-MUST-NOT-APPEAR", failure.Metadata!, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", failure.Metadata!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(NotificationFailureCategory.RequestRejected.ToString(), metadata.RootElement.GetProperty("failureCategory").GetString());
        Assert.Equal(notification.NotificationId, metadata.RootElement.GetProperty("notificationId").GetGuid());
        Assert.Equal("RequestCreated", metadata.RootElement.GetProperty("sourceEventType").GetString());
        Assert.Equal(request.RequestId, metadata.RootElement.GetProperty("sourceEntityId").GetGuid());
    }

    private static PortalPrincipal Principal(string key)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Role, identity.TeamId, true);
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
