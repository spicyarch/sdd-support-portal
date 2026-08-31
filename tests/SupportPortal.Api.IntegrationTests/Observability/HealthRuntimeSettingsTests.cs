using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Api.Endpoints;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Settings;
using SupportPortal.Domain.Notifications;
using SupportPortal.Domain.Settings;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Observability;

public sealed class HealthRuntimeSettingsTests
{
    [Fact]
    public void HealthReportsRuntimeDisablementWithoutDroppingDurableCounts()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemoryPortalStore(seed: false);
        var notification = new Notification(
            Guid.NewGuid(),
            NotificationEventType.RequestCreated,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            null,
            now,
            "correlation");
        store.AddNotification(notification);
        store.AddNotificationDelivery(new NotificationDelivery(
            Guid.NewGuid(),
            notification.NotificationId,
            NotificationRecipientKind.ConfiguredGlobalMailbox,
            null,
            "recipient@example.test",
            "recipient-key",
            now));
        var runtime = new RuntimeSettingsState(CreateRuntimeSnapshot(now));
        var endpoint = new HealthEndpoint(
            new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], now),
            new BrandingConfigurationStatus([], now),
            store,
            runtime);

        var result = endpoint.Run(CreateContext().Request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(objectResult.Value));
        var delivery = document.RootElement.GetProperty("emailDelivery");
        Assert.Equal("Disabled", delivery.GetProperty("state").GetString());
        Assert.Equal(1, delivery.GetProperty("pending").GetInt32());
        Assert.Single(store.GetNotificationDeliveries(notification.NotificationId));
        Assert.DoesNotContain("recipient@example.test", document.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static DefaultHttpContext CreateContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(new AzureOptions { AllowedOrigins = [] })
            .BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private static EffectiveSettingsSnapshot CreateRuntimeSnapshot(DateTimeOffset loadedAt)
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
            "disabled-revision",
            SettingsSource.AdministratorOverride,
            brand,
            "http://localhost:5258/invitations/accept",
            72,
            new EffectiveSendGridSettings(
                false,
                null,
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
            new RuntimeEmailAvailability(RuntimeEmailAvailabilityState.Disabled, [], loadedAt),
            false,
            SettingsApiKeyMode.Cleared,
            loadedAt);
    }
}
