using Microsoft.Azure.Functions.Worker;
using SupportPortal.Application.Notifications;

namespace SupportPortal.Api.Functions;

public sealed class NotificationDeliveryFunction(NotificationDeliveryProcessor processor)
{
    [Function("ProcessNotificationDeliveries")]
    public Task Run(
        [TimerTrigger("*/5 * * * * *", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken) =>
        processor.ProcessOnceAsync(cancellationToken);
}