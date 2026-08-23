using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Application.Abstractions;
using SupportPortal.Infrastructure.Persistence;
using SupportPortal.Infrastructure.Persistence.Bootstrap;

namespace SupportPortal.Infrastructure.Configuration;

public static class PortalPersistenceRegistration
{
    public static IServiceCollection AddPortalPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<PortalDatabaseInitializer>();
        var sqlConnection = configuration["Portal:SqlConnection"];
        if (string.IsNullOrWhiteSpace(sqlConnection))
        {
            services.AddSingleton<IPortalStore, InMemoryPortalStore>();
            return services;
        }

        services.AddDbContext<SupportPortalDbContext>(options =>
            options.UseSqlServer(sqlConnection, sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IPortalStore, EfPortalStore>();
        return services;
    }
}
