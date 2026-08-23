using SupportPortal.Application;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Common;
using SupportPortal.Contracts.Requests;
using SupportPortal.Domain.Authorization;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Application.Tests.Requests;

public sealed class GlobalSupportWorkflowTests
{
    [Fact]
    public void GlobalSupportCanAssignActiveGlobalUserButNotTeamUser()
    {
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(store, TimeProvider.System);
        var creator = Principal("team-user-a");
        var support = Principal("global-support");
        var teamUser = Principal("team-user-a");
        var request = service.CreateRequest(creator, Guid.NewGuid(), new CreateSupportRequestRequest("Assignment request", "Normal", "Validate assignment rules."));

        var assigned = service.AssignRequest(
            support,
            request.RequestId,
            request.RowVersion,
            Guid.NewGuid(),
            new AssignRequestRequest(support.UserId));

        Assert.Equal(support.UserId, assigned.AssigneeUserId);
        var exception = Assert.Throws<PortalServiceException>(() => service.AssignRequest(
            support,
            request.RequestId,
            assigned.RowVersion,
            Guid.NewGuid(),
            new AssignRequestRequest(teamUser.UserId)));
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void StaleGlobalSupportUpdateIsRejected()
    {
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(store, TimeProvider.System);
        var creator = Principal("team-user-a");
        var support = Principal("global-support");
        var request = service.CreateRequest(creator, Guid.NewGuid(), new CreateSupportRequestRequest("Concurrency request", "Normal", "Validate ETags."));

        var changed = service.ChangePriority(support, request.RequestId, request.RowVersion, Guid.NewGuid(), new ChangeRequestPriorityRequest("High"));
        var exception = Assert.Throws<PortalServiceException>(() => service.ChangePriority(
            support,
            request.RequestId,
            request.RowVersion,
            Guid.NewGuid(),
            new ChangeRequestPriorityRequest("Urgent")));

        Assert.Equal("High", changed.Priority);
        Assert.Equal(412, exception.StatusCode);
    }

    private static PortalPrincipal Principal(string key)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Role, identity.TeamId, true);
    }
}
