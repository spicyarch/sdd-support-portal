using Microsoft.EntityFrameworkCore;
using SupportPortal.Application.Authorization;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Teams;
using SupportPortal.Infrastructure.Persistence;
using Xunit;

namespace SupportPortal.Api.IntegrationTests.Persistence;

internal static class SqlTestSupport
{
    public static SupportPortalDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable("SUPPORT_PORTAL_SQL_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("SUPPORT_PORTAL_SQL_TEST_CONNECTION is required for an SQL integration test.");
        }

        var options = new DbContextOptionsBuilder<SupportPortalDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var context = new SupportPortalDbContext(options);
        context.Database.Migrate();
        return context;
    }

    public static PortalPrincipal SeedTeamActor(EfPortalStore store, DateTimeOffset now)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var team = new Team(teamId, $"SQL Notification Test {suffix}", now);
        var user = new User(userId, tenantId, objectId, "SQL Notification Actor", $"actor-{suffix}@example.test", now);
        var assignment = new RoleAssignment(Guid.NewGuid(), userId, PortalRole.TeamUser, teamId, null, now);
        store.Execute(() =>
        {
            store.AddTeam(team);
            store.AddUser(user);
            store.AddRoleAssignment(assignment);
        });

        return new PortalPrincipal(userId, tenantId, objectId, user.DisplayName, PortalRole.TeamUser, teamId, true);
    }
}

internal sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPPORT_PORTAL_SQL_TEST_CONNECTION")))
        {
            Skip = "Set SUPPORT_PORTAL_SQL_TEST_CONNECTION to an approved dedicated SQL Server database to run SQL integration tests.";
        }
    }
}
