using SupportPortal.Application;
using SupportPortal.Application.Authorization;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Domain.Authorization;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Application.Tests.Administration;

public sealed class InvitationServiceTests
{
    [Fact]
    public void InvitationCanBeAcceptedOnceAndMatchingRetryReplaysTheResult()
    {
        var store = new InMemoryPortalStore();
        var service = new SupportPortalService(store, TimeProvider.System);
        var administrator = Principal("global-admin");
        var createKey = Guid.NewGuid();

        var created = service.CreateInvitation(
            administrator,
            createKey,
            new CreateInvitationRequest("new-user@example.test", "TeamUser", DevelopmentIdentities.TeamAId));
        var token = Uri.UnescapeDataString(created.AcceptanceLink[(created.AcceptanceLink.IndexOf("token=", StringComparison.Ordinal) + 6)..]);
        var identity = new AuthenticatedIdentity(
            DevelopmentIdentities.TenantId,
            Guid.NewGuid(),
            "New User",
            "new-user@example.test");
        var acceptKey = Guid.NewGuid();

        var accepted = service.AcceptInvitation(identity, acceptKey, new AcceptInvitationRequest(token));
        var replay = service.AcceptInvitation(identity, acceptKey, new AcceptInvitationRequest(token));

        Assert.Equal(accepted.UserId, replay.UserId);
        Assert.Equal("TeamUser", accepted.Role);
        Assert.Single(store.GetRoleAssignments(), assignment => assignment.UserId == accepted.UserId && assignment.IsActive);
        Assert.Contains(store.GetAuditEvents(), item => item.EventType == "InvitationAccepted");
    }

    private static PortalPrincipal Principal(string key)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Role, identity.TeamId, true);
    }
}
