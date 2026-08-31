using SupportPortal.Domain.Authorization;

namespace SupportPortal.Application.Authorization;

public sealed class PortalAccessEvaluator
{
    public bool CanReadTeam(PortalPrincipal principal, Guid teamId) =>
        principal.IsGlobal || principal.TeamId == teamId;

    public bool CanReadRequest(PortalPrincipal principal, Guid teamId) =>
        CanReadTeam(principal, teamId);

    public bool CanPostMessage(PortalPrincipal principal, Guid teamId) =>
        principal.IsActive && CanReadTeam(principal, teamId);

    public bool CanManageTeams(PortalPrincipal principal) => principal.IsGlobalAdministrator;

    public bool CanManageGlobalAccess(PortalPrincipal principal) => principal.IsGlobalAdministrator;

    public bool CanManageSettings(PortalPrincipal principal) =>
        principal.IsActive && principal.IsGlobalAdministrator;

    public bool CanManageTeamUsers(PortalPrincipal principal, Guid teamId) =>
        principal.IsGlobalAdministrator || (principal.IsTeamAdministrator && principal.TeamId == teamId);
}