using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SupportPortal.Infrastructure.Persistence.Bootstrap;

public sealed class PortalDatabaseInitializer(IServiceProvider services)
{
    public void ApplyMigrations()
    {
        var dbContext = services.GetService<SupportPortalDbContext>();
        dbContext?.Database.Migrate();
    }
}
