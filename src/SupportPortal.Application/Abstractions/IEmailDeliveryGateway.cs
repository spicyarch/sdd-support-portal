using SupportPortal.Application.Notifications;

namespace SupportPortal.Application.Abstractions;

public interface IEmailDeliveryGateway
{
    Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken);
}