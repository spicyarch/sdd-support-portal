using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.Authorization;

public static class RoleAssignmentPolicy
{
    public static void ValidateScope(PortalRole role, Guid? teamId)
    {
        var isGlobal = role is PortalRole.GlobalAdministrator or PortalRole.GlobalSupportUser;
        if (isGlobal && teamId is not null)
        {
            throw new DomainException("Global roles cannot have a team scope.");
        }

        if (!isGlobal && teamId is null)
        {
            throw new DomainException("Team roles require an active team scope.");
        }
    }

    public static bool IsGlobal(PortalRole role) =>
        role is PortalRole.GlobalAdministrator or PortalRole.GlobalSupportUser;

    public static bool CanManageAllTeams(PortalRole role) => role == PortalRole.GlobalAdministrator;

    public static bool CanManageRequestsAcrossTeams(PortalRole role) => IsGlobal(role);
}