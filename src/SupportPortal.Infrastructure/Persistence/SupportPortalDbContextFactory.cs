using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SupportPortal.Infrastructure.Persistence;

public sealed class SupportPortalDbContextFactory : IDesignTimeDbContextFactory<SupportPortalDbContext>
{
    public SupportPortalDbContext CreateDbContext(string[] args)
    {
        var connectionString = GetConnectionString(args) ?? throw new InvalidOperationException(
            "Set Portal__SqlConnection or pass --connection <connection-string> when creating the portal DbContext.");
        var options = new DbContextOptionsBuilder<SupportPortalDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;
        return new SupportPortalDbContext(options);
    }

    private static string? GetConnectionString(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--connection", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return Environment.GetEnvironmentVariable("Portal__SqlConnection");
    }
}
