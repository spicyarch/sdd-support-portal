using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Api.Endpoints;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;
using SupportPortal.Domain.Notifications;

namespace SupportPortal.Api.IntegrationTests.Observability;

public sealed class HealthEndpointTests
{
    [Fact]
    public void HealthReturnsSafeBrandingAndDeliveryStateBreakdown()
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

        var pending = Delivery(notification, now);
        var retryable = Delivery(notification, now);
        var retryAttempt = retryable.StartAttempt(Guid.NewGuid(), "retry-worker", now, TimeSpan.FromMinutes(1));
        retryable.MarkRetryable(429, NotificationFailureCategory.RateLimited, now, now);
        var sent = Delivery(notification, now);
        sent.StartAttempt(Guid.NewGuid(), "sent-worker", now, TimeSpan.FromMinutes(1));
        sent.MarkAccepted("provider-id", now);
        var permanent = Delivery(notification, now);
        permanent.StartAttempt(Guid.NewGuid(), "failed-worker", now, TimeSpan.FromMinutes(1));
        permanent.MarkPermanent(400, NotificationFailureCategory.RequestRejected, now);
        var suppressed = Delivery(notification, now);
        suppressed.MarkSuppressed(NotificationFailureCategory.Suppressed, now);

        store.AddNotificationDelivery(pending);
        store.AddNotificationDelivery(retryable);
        store.AddNotificationAttempt(retryAttempt);
        store.AddNotificationDelivery(sent);
        store.AddNotificationDelivery(permanent);
        store.AddNotificationDelivery(suppressed);

        var context = CreateContext();
        var endpoint = new HealthEndpoint(
            new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], now),
            new BrandingConfigurationStatus(["Branding:PrimaryColor"], now),
            store);

        var result = endpoint.Run(context.Request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(objectResult.Value));
        var root = document.RootElement;
        Assert.Equal("Fallback", root.GetProperty("branding").GetProperty("state").GetString());
        Assert.Equal("Branding:PrimaryColor", root.GetProperty("branding").GetProperty("invalidSettingNames")[0].GetString());
        var delivery = root.GetProperty("emailDelivery");
        Assert.Equal(1, delivery.GetProperty("pending").GetInt32());
        Assert.Equal(1, delivery.GetProperty("retryable").GetInt32());
        Assert.Equal(1, delivery.GetProperty("sent").GetInt32());
        Assert.Equal(1, delivery.GetProperty("permanent").GetInt32());
        Assert.Equal(1, delivery.GetProperty("suppressed").GetInt32());
        Assert.DoesNotContain("recipientAddress", document.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static NotificationDelivery Delivery(Notification notification, DateTimeOffset now) =>
        new(Guid.NewGuid(), notification.NotificationId, NotificationRecipientKind.ConfiguredGlobalMailbox, null, "recipient@example.test", Guid.NewGuid().ToString("N"), now);

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
}
