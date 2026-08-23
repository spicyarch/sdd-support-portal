using SupportPortal.Application;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Common;
using SupportPortal.Contracts.Requests;
using SupportPortal.Domain.Authorization;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests;

public sealed class SupportPortalServiceIntegrationTests
{
    [Fact]
    public void TeamIsolationAndAuditHistoryArePreservedTogether()
    {
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(store, TimeProvider.System);
        var teamA = Principal("team-user-a", PortalRole.TeamUser, DevelopmentIdentities.TeamAId);
        var teamB = Principal("team-user-b", PortalRole.TeamUser, DevelopmentIdentities.TeamBId);
        var request = service.CreateRequest(teamA, Guid.NewGuid(), new CreateSupportRequestRequest("Integration request", "High", "Keep this within Team A."));
        service.PostMessage(teamA, request.RequestId, Guid.NewGuid(), new CreateMessageRequest("Audit this reply.", Guid.NewGuid()));

        var exception = Assert.Throws<PortalServiceException>(() => service.GetRequest(teamB, request.RequestId));

        Assert.Equal(404, exception.StatusCode);
        Assert.Contains(store.GetAuditEvents(), item => item.EventType == "RequestCreated" && item.TargetId == request.RequestId);
        Assert.Contains(store.GetAuditEvents(), item => item.EventType == "MessagePosted" && item.TargetId == request.RequestId);
    }

    private static PortalPrincipal Principal(string key, PortalRole role, Guid? teamId)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, role, teamId, true);
    }
}