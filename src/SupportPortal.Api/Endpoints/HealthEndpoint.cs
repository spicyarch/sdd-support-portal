using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Middleware;
using SupportPortal.Application.Abstractions;
using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Api.Endpoints;

public sealed class HealthEndpoint(
    EmailDeliveryAvailability availability,
    BrandingConfigurationStatus brandingStatus,
    IPortalStore store)
{
    [Function("Health")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest request)
    {
        return ApiResponse.Json(request, new
        {
            status = availability.State == EmailDeliveryState.InvalidConfiguration ? "degraded" : "ok",
            service = "support-portal-api",
            traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier,
            branding = new
            {
                state = brandingStatus.UsesFallbacks ? "Fallback" : "Ready",
                invalidSettingNames = brandingStatus.InvalidSettingNames,
                checkedAt = brandingStatus.CheckedAt
            },
            emailDelivery = new
            {
                state = availability.State.ToString(),
                invalidSettingNames = availability.InvalidSettingNames,
                pending = SafeCount(() => store.GetNotificationDeliveriesByState(Domain.Notifications.NotificationDeliveryState.Pending)),
                retryable = SafeCount(() => store.GetNotificationDeliveriesByState(Domain.Notifications.NotificationDeliveryState.RetryableFailure)),
                sent = SafeCount(() => store.GetNotificationDeliveriesByState(Domain.Notifications.NotificationDeliveryState.Sent)),
                permanent = SafeCount(() => store.GetNotificationDeliveriesByState(Domain.Notifications.NotificationDeliveryState.PermanentFailure)),
                suppressed = SafeCount(() => store.GetNotificationDeliveriesByState(Domain.Notifications.NotificationDeliveryState.Suppressed)),
                checkedAt = availability.CheckedAt
            }
        });
    }

    private static int SafeCount(Func<int> count)
    {
        try
        {
            return count();
        }
        catch
        {
            return 0;
        }
    }
}