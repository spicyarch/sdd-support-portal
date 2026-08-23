using Microsoft.EntityFrameworkCore;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.SupportRequests;
using SupportPortal.Domain.Teams;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Persistence;

public sealed class EfPortalStoreTests
{
    [Fact]
    public void PersistsRequestMessagesAndCommandReceiptsAcrossContexts()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SupportPortalDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var writeContext = new SupportPortalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            var store = new EfPortalStore(writeContext);
            var user = new User(userId, Guid.NewGuid(), Guid.NewGuid(), "Test User", "test@example.com", now);
            var team = new Team(teamId, "Test Team", now);
            var request = new SupportRequest(requestId, "SP-TEST01", teamId, userId, "Durable request", "Stored in the EF-backed portal store.", RequestPriority.High, now);
            request.AddMessage(new Message(Guid.NewGuid(), requestId, userId, PortalRole.TeamUser, "Persist this message.", Guid.NewGuid(), now.AddSeconds(1)), now.AddSeconds(1));
            var receipt = new CommandReceipt(Guid.NewGuid(), userId, idempotencyKey, "fingerprint", 201, "{\"requestId\":\"stored\"}", now);

            store.Execute(() =>
            {
                store.AddUser(user);
                store.AddTeam(team);
                store.AddRoleAssignment(new RoleAssignment(Guid.NewGuid(), userId, PortalRole.TeamUser, teamId, null, now));
                store.AddRequest(request);
                store.AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "RequestCreated", userId, "SupportRequest", requestId, true));
                store.AddCommandReceipt(receipt);
            });
        }

        using var readContext = new SupportPortalDbContext(options);
        var readStore = new EfPortalStore(readContext);
        var persistedRequest = readStore.GetRequest(requestId);
        var persistedReceipt = readStore.GetCommandReceipt(userId, idempotencyKey);

        Assert.NotNull(persistedRequest);
        Assert.Single(persistedRequest!.Messages);
        Assert.Equal("Persist this message.", persistedRequest.Messages[0].Body);
        Assert.NotNull(persistedReceipt);
        Assert.Equal(201, persistedReceipt!.ResponseStatus);
    }
}
