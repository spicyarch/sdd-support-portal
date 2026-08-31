using SupportPortal.Application;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Notifications;
using SupportPortal.Application.Settings;
using SupportPortal.Contracts.Requests;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Notifications;
using SupportPortal.Domain.Settings;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Application.Tests.Notifications;

public sealed class NotificationRuntimeSettingsTests
{
    [Fact]
    public async Task RuntimeDisablePreservesPendingWorkUntilValidSettingsReturn()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-24T00:00:00Z"));
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(
            store,
            clock,
            notificationScheduler: new NotificationScheduler(store, enabled: true));
        service.CreateRequest(
            Principal("team-user-a"),
            Guid.NewGuid(),
            new CreateSupportRequestRequest("Runtime disable", "Normal", "Description"));
        var notification = Assert.Single(store.GetNotifications());
        var gateway = new FakeEmailDeliveryGateway();
        var runtime = new RuntimeSettingsState(CreateRuntimeSnapshot(false, clock.GetUtcNow()));
        var processor = CreateProcessor(store, clock, gateway, runtime);

        Assert.Equal(0, await processor.ProcessOnceAsync());
        Assert.Empty(gateway.Requests);
        Assert.Equal(NotificationStatus.PendingRecipients, notification.Status);
        Assert.Empty(store.GetNotificationDeliveries(notification.NotificationId));

        runtime.Publish(CreateRuntimeSnapshot(true, clock.GetUtcNow()), clock.GetUtcNow());
        Assert.Equal(1, await processor.ProcessOnceAsync());
        Assert.Single(gateway.Requests);
        Assert.Single(store.GetNotifications());
        Assert.Equal(NotificationDeliveryState.Sent, Assert.Single(store.GetNotificationDeliveries(notification.NotificationId)).State);
    }

    private static NotificationDeliveryProcessor CreateProcessor(
        InMemoryPortalStore store,
        MutableTimeProvider clock,
        FakeEmailDeliveryGateway gateway,
        RuntimeSettingsState runtime)
    {
        var brand = BrandingResolver.Resolve(new BrandingInput(
            "Support Portal",
            "SP",
            null,
            null,
            null,
            null,
            null,
            "Support",
            "support@example.test",
            null), "Development");
        return new NotificationDeliveryProcessor(
            store,
            gateway,
            new NotificationRecipientPlanner(store, runtime),
            new NotificationMessageComposer(store, brand, "http://localhost:5258", new FakeInvitationTokenService(), runtime),
            new NotificationRetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), new Random(1), runtime),
            clock,
            TimeSpan.FromSeconds(1),
            enabled: true,
            canSend: true,
            batchSize: 25,
            runtime);
    }

    private static PortalPrincipal Principal(string key)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Role, identity.TeamId, true);
    }

    private static EffectiveSettingsSnapshot CreateRuntimeSnapshot(bool enabled, DateTimeOffset loadedAt)
    {
        var brand = BrandingResolver.Resolve(new BrandingInput(
            "Support Portal",
            "SP",
            null,
            null,
            null,
            null,
            null,
            "Support",
            "support@example.test",
            null), "Development");
        return new EffectiveSettingsSnapshot(
            enabled ? "enabled-revision" : "disabled-revision",
            SettingsSource.AdministratorOverride,
            brand,
            "http://localhost:5258/invitations/accept",
            72,
            new EffectiveSendGridSettings(
                enabled,
                enabled ? "runtime-key" : null,
                "Support Portal",
                "sender@example.test",
                "support@example.test",
                ["global-support@example.test"],
                "http://localhost:5258",
                15,
                4,
                5,
                60,
                "Global",
                25,
                60),
            new RuntimeEmailAvailability(
                enabled ? RuntimeEmailAvailabilityState.Ready : RuntimeEmailAvailabilityState.Disabled,
                [],
                loadedAt),
            enabled,
            enabled ? SettingsApiKeyMode.Managed : SettingsApiKeyMode.Cleared,
            loadedAt);
    }

    private sealed class FakeEmailDeliveryGateway : IEmailDeliveryGateway
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

    private sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
    }
}
