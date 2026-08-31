using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Middleware;
using SupportPortal.Application.Abstractions;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Application.Settings;

namespace SupportPortal.Api.Endpoints;

public sealed class HealthEndpoint(
    EmailDeliveryAvailability availability,
    BrandingConfigurationStatus brandingStatus,
    IPortalStore store,
    RuntimeSettingsState? runtimeSettings = null,
    SettingsRefreshCoordinator? refreshCoordinator = null)
{
    [Function("Health")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest request)
    {
        refreshCoordinator?.RefreshIfDueAsync(request.HttpContext.RequestAborted).GetAwaiter().GetResult();
        var currentAvailability = runtimeSettings is null
            ? availability
            : new EmailDeliveryAvailability(
                (EmailDeliveryState)runtimeSettings.Current.EmailAvailability.State,
                runtimeSettings.Current.EmailAvailability.InvalidSettingNames,
                runtimeSettings.Current.EmailAvailability.CheckedAt);
        var activation = runtimeSettings?.Activation;
        return ApiResponse.Json(request, new
        {
            status = currentAvailability.State == EmailDeliveryState.InvalidConfiguration ? "degraded" : "ok",
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
                state = currentAvailability.State.ToString(),
                invalidSettingNames = currentAvailability.InvalidSettingNames,
                pending = SafeCount(() => store.GetNotificationDeliveriesByState(Domain.Notifications.NotificationDeliveryState.Pending)),
                retryable = SafeCount(() => store.GetNotificationDeliveriesByState(Domain.Notifications.NotificationDeliveryState.RetryableFailure)),
                sent = SafeCount(() => store.GetNotificationDeliveriesByState(Domain.Notifications.NotificationDeliveryState.Sent)),
                permanent = SafeCount(() => store.GetNotificationDeliveriesByState(Domain.Notifications.NotificationDeliveryState.PermanentFailure)),
                suppressed = SafeCount(() => store.GetNotificationDeliveriesByState(Domain.Notifications.NotificationDeliveryState.Suppressed)),
                checkedAt = currentAvailability.CheckedAt
            },
            settingsActivation = activation is null ? null : new
            {
                state = activation.State.ToString(),
                activeVersion = activation.ActiveVersion,
                desiredVersion = activation.DesiredVersion,
                lastAttemptAt = activation.LastAttemptAt,
                lastSuccessfulAt = activation.LastSuccessfulAt,
                failureCategory = activation.FailureCategory,
                invalidSettingNames = activation.InvalidSettingNames
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