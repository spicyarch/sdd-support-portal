using SupportPortal.Application.Notifications;

namespace SupportPortal.Application.Abstractions;

public interface IEmailReadinessGateway
{
    Task<EmailReadinessResult> CheckAsync(EmailReadinessRequest request, string correlationId, CancellationToken cancellationToken);
}