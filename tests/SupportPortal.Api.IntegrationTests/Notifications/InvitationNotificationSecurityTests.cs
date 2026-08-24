using System.Security.Cryptography;
using System.Text.Json;
using SupportPortal.Application;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Common;
using SupportPortal.Application.Notifications;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Notifications;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;
using SupportPortal.Infrastructure.Persistence.Bootstrap;

namespace SupportPortal.Api.IntegrationTests.Notifications;

public sealed class InvitationNotificationSecurityTests
{
    [Fact]
    public async Task ConfiguredTokenIsReconstructedForDeliveryButAbsentFromDurableFeatureData()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-24T00:00:00Z"));
        var options = CreateTokenOptions();
        var tokens = new ConfiguredInvitationTokenService(options);
        var store = new InMemoryPortalStore(seed: false);
        var scheduler = new NotificationScheduler(store, enabled: true);
        var service = new SupportPortalService(store, clock, tokens, scheduler);
        var creator = Principal("Creator");
        var invitationResponse = service.CreateInvitation(
            creator,
            Guid.NewGuid(),
            new CreateInvitationRequest("new-user@example.test", "GlobalSupportUser", null));
        var invitation = store.GetInvitation(invitationResponse.InvitationId)!;
        var token = tokens.CreateToken(invitation.InvitationId);

        Assert.Contains(token, invitationResponse.AcceptanceLink, StringComparison.Ordinal);
        Assert.DoesNotContain(token, DurableFeatureData(store), StringComparison.Ordinal);

        var gateway = new RecordingGateway();
        await CreateProcessor(store, clock, tokens, gateway).ProcessOnceAsync();

        var message = Assert.Single(gateway.Requests);
        Assert.Contains(token, message.PlainTextContent, StringComparison.Ordinal);
        Assert.Contains(token, message.HtmlContent, StringComparison.Ordinal);
        Assert.DoesNotContain(token, DurableFeatureData(store), StringComparison.Ordinal);

        var identity = new AuthenticatedIdentity(
            TestTenantId,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "New User",
            "new-user@example.test");
        var accepted = service.AcceptInvitation(identity, Guid.NewGuid(), new AcceptInvitationRequest(token));

        Assert.Equal("GlobalSupportUser", accepted.Role);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        var secondAttempt = Assert.Throws<PortalServiceException>(() => service.AcceptInvitation(
            identity,
            Guid.NewGuid(),
            new AcceptInvitationRequest(token)));
        Assert.Equal(409, secondAttempt.StatusCode);
        Assert.DoesNotContain(token, DurableFeatureData(store), StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledDeliveryKeepsInvitationWorkflowAvailableWithoutOutboxRows()
    {
        var options = CreateTokenOptions();
        var tokens = new ConfiguredInvitationTokenService(options);
        var store = new InMemoryPortalStore(seed: false);
        var service = new SupportPortalService(
            store,
            TimeProvider.System,
            tokens,
            new NotificationScheduler(store, enabled: false));

        var response = service.CreateInvitation(
            Principal("Creator"),
            Guid.NewGuid(),
            new CreateInvitationRequest("disabled@example.test", "GlobalSupportUser", null));

        Assert.Equal("Pending", response.State);
        Assert.Empty(store.GetNotifications());
        Assert.NotEmpty(response.AcceptanceLink);
    }

    private static NotificationDeliveryProcessor CreateProcessor(
        InMemoryPortalStore store,
        MutableTimeProvider clock,
        IInvitationTokenService tokens,
        RecordingGateway gateway)
    {
        var brand = BrandingResolver.Resolve(
            new BrandingInput("Support Portal", "SP", null, null, null, null, null, "Support", "support@example.test", null),
            "Development");
        return new NotificationDeliveryProcessor(
            store,
            gateway,
            new NotificationRecipientPlanner(store, []),
            new NotificationMessageComposer(store, brand, "https://portal.example", tokens),
            new NotificationRetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), new Random(1)),
            clock,
            TimeSpan.FromMinutes(1),
            enabled: true,
            canSend: true,
            batchSize: 25);
    }

    private static AzureOptions CreateTokenOptions() => new()
    {
        AuthenticationMode = "Entra",
        InvitationTokenKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        InvitationAcceptanceBaseUrl = "https://portal.example/invitations/accept",
        InvitationLifetimeHours = 72
    };

    private static PortalPrincipal Principal(string displayName) =>
        new(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            TestTenantId,
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            displayName,
            PortalRole.GlobalAdministrator,
            null,
            true);

    private static readonly Guid TestTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static string DurableFeatureData(InMemoryPortalStore store)
    {
        var attempts = store.GetNotifications()
            .SelectMany(notification => store.GetNotificationDeliveries(notification.NotificationId))
            .SelectMany(delivery => store.GetNotificationAttempts(delivery.NotificationDeliveryId));
        return JsonSerializer.Serialize(new
        {
            invitations = store.GetInvitations(),
            notifications = store.GetNotifications(),
            deliveries = store.GetNotifications().SelectMany(notification => store.GetNotificationDeliveries(notification.NotificationId)),
            attempts,
            audits = store.GetAuditEvents(),
            receipts = store.GetCommandReceipts()
        });
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

    private sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
    }
}
