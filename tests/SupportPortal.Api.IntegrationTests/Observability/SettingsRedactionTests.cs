using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Api.Endpoints;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Observability;

public sealed class SettingsRedactionTests
{
    [Fact]
    public void HealthDiagnosticsDoNotExposeSecretConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["Portal:AllowedOrigins"] = "http://localhost:5258"
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(new AzureOptions { AllowedOrigins = [] })
            .BuildServiceProvider();
        var request = new DefaultHttpContext { RequestServices = services }.Request;
        var endpoint = new HealthEndpoint(
            new EmailDeliveryAvailability(EmailDeliveryState.InvalidConfiguration, ["SendGrid:ApiKey"], DateTimeOffset.UtcNow),
            new BrandingConfigurationStatus([], DateTimeOffset.UtcNow),
            new InMemoryPortalStore(seed: false));

        var result = endpoint.Run(request);

        var response = Assert.IsType<ObjectResult>(result);
        var payload = JsonSerializer.Serialize(response.Value);
        Assert.DoesNotContain("ApiKeyValue", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SendGrid:ApiKey", payload, StringComparison.Ordinal);
    }
}
