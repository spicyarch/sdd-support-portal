using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.Tests.Authorization;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public void GlobalRoleCannotHaveTeamScope()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new RoleAssignment(Guid.NewGuid(), Guid.NewGuid(), PortalRole.GlobalSupportUser, Guid.NewGuid(), null, DateTimeOffset.UtcNow));

        Assert.Contains("team scope", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TeamRoleRequiresTeamScope()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new RoleAssignment(Guid.NewGuid(), Guid.NewGuid(), PortalRole.TeamUser, null, null, DateTimeOffset.UtcNow));

        Assert.Contains("team scope", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FinalGlobalAdministratorCannotBeRemoved()
    {
        var exception = Assert.Throws<DomainException>(() =>
            LastGlobalAdministratorPolicy.EnsureAnotherAdministratorRemains(1, true));

        Assert.Contains("final active", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}