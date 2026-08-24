using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Common;
using OperationsEmailReadinessRequest = SupportPortal.Contracts.Operations.EmailReadinessRequest;

namespace SupportPortal.Application.Notifications;

public sealed class EmailReadinessService
{
    private readonly IEmailReadinessGateway gateway;

    public EmailReadinessService(IEmailReadinessGateway gateway)
    {
        this.gateway = gateway;
    }

    public Task<EmailReadinessResult> CheckAsync(
        PortalPrincipal principal,
        OperationsEmailReadinessRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!principal.IsActive || !principal.IsGlobalAdministrator)
        {
            throw new PortalServiceException(403, "Forbidden", "Only a Global Administrator can run an email readiness check.");
        }

        if (!Enum.TryParse<EmailReadinessMode>(request.Mode, ignoreCase: true, out var mode))
        {
            throw new PortalServiceException(400, "Invalid readiness mode", "Mode must be Sandbox or Live.");
        }

        if (mode == EmailReadinessMode.Live &&
            (string.IsNullOrWhiteSpace(request.TestRecipient) ||
             !TryValidateEmail(request.TestRecipient) ||
             !request.ConfirmLiveSend))
        {
            throw new PortalServiceException(400, "Invalid live readiness request", "Live readiness requires a valid test recipient and explicit confirmation.");
        }

        return gateway.CheckAsync(
            new EmailReadinessRequest(mode, request.TestRecipient?.Trim(), request.ConfirmLiveSend),
            correlationId,
            cancellationToken);
    }

    private static bool TryValidateEmail(string value)
    {
        return EmailAddressRules.TryNormalize(value, out _);
    }
}