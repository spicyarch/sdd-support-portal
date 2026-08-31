using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Api.Endpoints;
using SupportPortal.Application.Branding;
using SupportPortal.Contracts.Branding;
using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Api.IntegrationTests.Branding;

public sealed class BrandingEndpointTests
{
    [Fact]
    public void AnonymousRequestReturnsOnlyTheEffectivePublicBrand()
    {
        var brand = BrandingResolver.Resolve(
            new BrandingInput(
                "Northwind Support",
                "NS",
                "https://cdn.example.test/logo.png",
                "https://cdn.example.test/favicon.png",
                "#135E96",
                "#006B54",
                "#7A1F5B",
                "Operations",
                "support@example.test",
                "Northwind Traders"),
            "Production");
        var context = CreateContext();

        var result = new BrandingEndpoint(brand).Get(context.Request);

        var response = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<EffectiveBrandingResponse>(response.Value);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Northwind Support", payload.ProductName);
        Assert.Equal("Northwind Traders", payload.OrganizationName);
        Assert.Equal(brand.ProfileVersion, context.Response.Headers.ETag.ToString().Trim('"'));
        Assert.Equal("public, max-age=30", context.Response.Headers.CacheControl.ToString());
        Assert.DoesNotContain("SendGrid", JsonSerializer.Serialize(payload), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", JsonSerializer.Serialize(payload), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MatchingEtagReturnsNotModifiedWithTheSameOpaqueVersion()
    {
        var brand = BrandingResolver.Resolve(new BrandingInput(null, null, null, null, null, null, null, null, null, null), "Production");
        var context = CreateContext();
        context.Request.Headers.IfNoneMatch = $"\"{brand.ProfileVersion}\"";

        var result = new BrandingEndpoint(brand).Get(context.Request);

        var response = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, response.StatusCode);
        Assert.Equal($"\"{brand.ProfileVersion}\"", context.Response.Headers.ETag.ToString());
        Assert.Equal("public, max-age=30", context.Response.Headers.CacheControl.ToString());
    }

    private static DefaultHttpContext CreateContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(new AzureOptions { AllowedOrigins = ["http://localhost:5258"] })
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers.Origin = "http://localhost:5258";
        return context;
    }
}
