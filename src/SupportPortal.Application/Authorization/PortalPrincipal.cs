using SupportPortal.Domain.Authorization;

namespace SupportPortal.Application.Authorization;

public sealed record PortalPrincipal(
    Guid UserId,
    Guid TenantId,
    Guid ObjectId,
    string DisplayName,
    PortalRole Role,
    Guid? TeamId,
    bool IsActive)
{
    public bool IsGlobal => RoleAssignmentPolicy.IsGlobal(Role);

    public bool IsGlobalAdministrator => Role == PortalRole.GlobalAdministrator;

    public bool IsTeamAdministrator => Role == PortalRole.TeamAdministrator;

    public bool CanCreateRequests => Role is PortalRole.TeamAdministrator or PortalRole.TeamUser;
}