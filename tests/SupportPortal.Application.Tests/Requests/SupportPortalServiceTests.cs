using SupportPortal.Application;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Common;
using SupportPortal.Contracts.Requests;
using SupportPortal.Infrastructure.Persistence;
using SupportPortal.Domain.Authorization;

namespace SupportPortal.Application.Tests.Requests;

public sealed class SupportPortalServiceTests
{
    [Fact]
    public void TeamUserCannotReadAnotherTeamsRequest()
    {
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(store, TimeProvider.System);
        var teamAUser = Principal("team-user-a", PortalRole.TeamUser, DevelopmentIdentities.TeamAId);
        var teamBUser = Principal("team-user-b", PortalRole.TeamUser, DevelopmentIdentities.TeamBId);
        var request = service.CreateRequest(teamAUser, Guid.NewGuid(), new CreateSupportRequestRequest("Scoped request", "Normal", "Team A only"));

        var exception = Assert.Throws<PortalServiceException>(() => service.GetRequest(teamBUser, request.RequestId));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public void RepeatingCreateWithSameIdempotencyKeyReturnsOneRequest()
    {
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(store, TimeProvider.System);
        var principal = Principal("team-user-a", PortalRole.TeamUser, DevelopmentIdentities.TeamAId);
        var key = Guid.NewGuid();
        var input = new CreateSupportRequestRequest("Retry request", "Normal", "Accepted exactly once");

        var first = service.CreateRequest(principal, key, input);
        var retry = service.CreateRequest(principal, key, input);

        Assert.Equal(first.RequestId, retry.RequestId);
        Assert.Single(store.GetRequests(), item => item.Reference == first.Reference);
    }

    [Fact]
    public void GlobalSupportCanAdvanceRequestStateWithCurrentVersion()
    {
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(store, TimeProvider.System);
        var creator = Principal("team-user-a", PortalRole.TeamUser, DevelopmentIdentities.TeamAId);
        var support = Principal("global-support", PortalRole.GlobalSupportUser, null);
        var request = service.CreateRequest(creator, Guid.NewGuid(), new CreateSupportRequestRequest("State request", "Normal", "Move through support"));

        var changed = service.ChangeState(support, request.RequestId, request.RowVersion, Guid.NewGuid(), new ChangeRequestStateRequest("InProgress"));

        Assert.Equal("InProgress", changed.Status);
        Assert.NotEqual(request.RowVersion, changed.RowVersion);
    }

    private static PortalPrincipal Principal(string key, PortalRole role, Guid? teamId)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, role, teamId, true);
    }
}