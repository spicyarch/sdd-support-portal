using SupportPortal.Application.Common;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;
using SupportPortal.Infrastructure.Persistence.Bootstrap;

namespace SupportPortal.Application.Tests.Administration;

public sealed class PortalBootstrapServiceTests
{
    [Fact]
    public void BootstrapCreatesConfiguredAdministratorAndReplaysOnlyMatchingRetry()
    {
        var tenantId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var store = new InMemoryPortalStore(seed: false);
        var service = new PortalBootstrapService(
            store,
            new AzureOptions
            {
                BootstrapEnabled = true,
                BootstrapTenantId = tenantId,
                BootstrapObjectId = objectId
            },
            TimeProvider.System);
        var key = Guid.NewGuid();
        var input = new BootstrapPortalRequest("First Administrator", "admin@example.com");

        var first = service.Bootstrap(key, input);
        var replay = service.Bootstrap(key, input);

        Assert.Equal(first.UserId, replay.UserId);
        Assert.Single(store.GetRoleAssignments(), assignment => assignment.IsActive);
        Assert.Equal(objectId, store.GetUser(first.UserId)!.ObjectId);
        Assert.Contains(store.GetAuditEvents(), item => item.EventType == "BootstrapCompleted");

        var exception = Assert.Throws<PortalServiceException>(() => service.Bootstrap(Guid.NewGuid(), input));
        Assert.Equal(409, exception.StatusCode);
    }
}
