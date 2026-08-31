using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Common;
using SupportPortal.Application.Settings;
using OperationsEmailReadinessRequest = SupportPortal.Contracts.Operations.EmailReadinessRequest;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;

namespace SupportPortal.Application.Notifications;

public sealed class EmailReadinessService
{
    private readonly IEmailReadinessGateway gateway;
    private readonly SettingsRefreshCoordinator? refreshCoordinator;
    private readonly IPortalStore? store;

    public EmailReadinessService(
        IEmailReadinessGateway gateway,
        SettingsRefreshCoordinator? refreshCoordinator = null,
        IPortalStore? store = null)
    {
        this.gateway = gateway;
        this.refreshCoordinator = refreshCoordinator;
        this.store = store;
    }

    public async Task<EmailReadinessResult> CheckAsync(
        PortalPrincipal principal,
        OperationsEmailReadinessRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureActiveGlobalAdministrator(principal);

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

        if (refreshCoordinator is not null)
        {
            await refreshCoordinator.RefreshIfDueAsync(cancellationToken);
        }

        EnsureActiveGlobalAdministrator(principal);
        var result = await gateway.CheckAsync(
            new EmailReadinessRequest(mode, request.TestRecipient?.Trim(), request.ConfirmLiveSend),
            correlationId,
            cancellationToken);
        RecordAudit(principal, result, correlationId);
        return result;
    }

    private void EnsureActiveGlobalAdministrator(PortalPrincipal principal)
    {
        if (!principal.IsActive || !principal.IsGlobalAdministrator)
        {
            throw new PortalServiceException(403, "Forbidden", "Only a Global Administrator can run an email readiness check.");
        }

        if (store is null)
        {
            return;
        }

        var user = store.GetUser(principal.UserId);
        var assignment = store.GetActiveRoleAssignment(principal.UserId);
        if (user?.Status != UserStatus.Active || assignment?.Role != PortalRole.GlobalAdministrator)
        {
            throw new PortalServiceException(403, "Forbidden", "Only a Global Administrator can run an email readiness check.");
        }
    }

    private void RecordAudit(
        PortalPrincipal principal,
        EmailReadinessResult result,
        string correlationId)
    {
        if (store is null)
        {
            return;
        }

        var settingsId = store.GetDeploymentSettings()?.DeploymentSettingsId ?? Guid.Empty;
        store.Execute(() => store.AddAuditEvent(new AuditEvent(
            Guid.NewGuid(),
            result.CheckedAt,
            SettingsAuditPolicy.EmailReadinessChecked,
            principal.UserId,
            SettingsAuditPolicy.TargetType,
            settingsId,
            result.Outcome is EmailReadinessOutcome.Ready or EmailReadinessOutcome.Accepted,
            SettingsAuditPolicy.CreateReadinessMetadata(result, correlationId))));
    }

    private static bool TryValidateEmail(string value)
    {
        return EmailAddressRules.TryNormalize(value, out _);
    }
}