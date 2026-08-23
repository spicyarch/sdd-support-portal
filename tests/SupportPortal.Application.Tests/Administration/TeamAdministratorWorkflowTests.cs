using SupportPortal.Application;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Common;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Domain.Authorization;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Application.Tests.Administration;

public sealed class TeamAdministratorWorkflowTests
{
    [Fact]
    public void TeamAdministratorCanProvisionOwnTeamUserButCannotGrantGlobalRole()
    {
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(store, TimeProvider.System);
        var administrator = Principal("team-admin-a");
        var newObjectId = Guid.NewGuid();

        var membership = service.CreateMembership(
            administrator,
            Guid.NewGuid(),
            new CreateMembershipRequest(newObjectId, "New Team User", "new-team-user@example.test", "TeamUser", DevelopmentIdentities.TeamAId));

        Assert.Equal("TeamUser", membership.Role);
        Assert.Equal(DevelopmentIdentities.TeamAId, membership.TeamId);

        var exception = Assert.Throws<PortalServiceException>(() => service.CreateMembership(
            administrator,
            Guid.NewGuid(),
            new CreateMembershipRequest(Guid.NewGuid(), "Escalation", "escalation@example.test", "GlobalSupportUser", null)));
        Assert.Equal(403, exception.StatusCode);
    }

    [Fact]
    public void FinalGlobalAdministratorCannotBeDeactivated()
    {
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(store, TimeProvider.System);
        var administrator = Principal("global-admin");
        var target = store.GetUser(administrator.UserId)!;

        var exception = Assert.Throws<PortalServiceException>(() => service.ChangeUserStatus(
            administrator,
            target.UserId,
            target.RowVersion,
            Guid.NewGuid(),
            new ChangeUserStatusRequest("Deactivated", "Test final administrator protection.")));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(UserStatus.Active, target.Status);
    }

    private static PortalPrincipal Principal(string key)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Role, identity.TeamId, true);
    }
}
