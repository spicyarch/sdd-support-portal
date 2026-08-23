using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Api.Auth;
using SupportPortal.Api.Middleware;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Common;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Security;

public sealed class ApiBoundaryTests
{
    [Fact]
    public void ProductionIdentityResolutionRequiresAnAuthenticatedHostPrincipal()
    {
        var configuration = Configuration("Production", false);
        var factory = new EntraClaimsPrincipalFactory(configuration, new InMemoryPortalStore());
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Development-Identity"] = "team-user-a";

        var exception = Assert.Throws<PortalServiceException>(() => factory.Resolve(context.Request));

        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public void ProductionIdentityResolutionValidatesConfiguredTenantAndAudience()
    {
        var configuration = Configuration("Production", false);
        var factory = new EntraClaimsPrincipalFactory(configuration, new InMemoryPortalStore());
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tid", DevelopmentIdentities.TenantId.ToString()),
                new Claim("oid", "20000000-0000-0000-0000-000000000004"),
                new Claim("aud", "api://support-portal"),
                new Claim("name", "Team A User"),
                new Claim("preferred_username", "team-user-a@example.test")
            ], "Bearer"))
        };

        var principal = factory.Resolve(context.Request);

        Assert.Equal("TeamUser", principal.Role.ToString());
        Assert.Equal(DevelopmentIdentities.TeamAId, principal.TeamId);
    }

    [Fact]
    public void CorsAllowsOnlyConfiguredOrigins()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration("Production", false));
        services.AddSingleton(new AzureOptions { AllowedOrigins = ["https://portal.example.test"] });
        var provider = services.BuildServiceProvider();

        var allowedContext = new DefaultHttpContext { RequestServices = provider };
        allowedContext.Request.Headers.Origin = "https://portal.example.test";
        ApiResponse.AddCorsHeaders(allowedContext.Request);

        var deniedContext = new DefaultHttpContext { RequestServices = provider };
        deniedContext.Request.Headers.Origin = "https://attacker.example.test";
        ApiResponse.AddCorsHeaders(deniedContext.Request);

        Assert.Equal("https://portal.example.test", allowedContext.Response.Headers.AccessControlAllowOrigin);
        Assert.False(deniedContext.Response.Headers.ContainsKey("Access-Control-Allow-Origin"));
        Assert.DoesNotContain("X-Development-Identity", deniedContext.Response.Headers.AccessControlAllowHeaders.ToString(), StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(string environment, bool developmentIdentitiesEnabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = environment,
                ["Portal:DevelopmentIdentitiesEnabled"] = developmentIdentitiesEnabled.ToString(),
                ["Entra:TenantId"] = DevelopmentIdentities.TenantId.ToString(),
                ["Entra:Audience"] = "api://support-portal"
            })
            .Build();
}
