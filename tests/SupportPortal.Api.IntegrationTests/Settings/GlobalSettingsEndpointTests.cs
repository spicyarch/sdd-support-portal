using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Api.Auth;
using SupportPortal.Api.Endpoints;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Settings;
using SupportPortal.Contracts.Settings;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Settings;

public sealed class GlobalSettingsEndpointTests
{
    [Fact]
    public async Task OnlyGlobalAdministratorCanReadSettings()
    {
        var fixture = CreateFixture();

        var result = await fixture.Endpoint.Get(fixture.CreateRequest("global-support"));

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.DoesNotContain("Northwind", JsonSerializer.Serialize(response.Value), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GlobalAdministratorReceivesRedactedSettingsAndEtag()
    {
        var fixture = CreateFixture();

        var result = await fixture.Endpoint.Get(fixture.CreateRequest("global-admin"));

        var response = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<GlobalSettingsResponse>(response.Value);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("Support Portal", payload.Branding.ProductName);
        Assert.Equal("Disabled", payload.EmailAvailability.State);
        Assert.False(payload.SendGrid.ApiKeyConfigured);
        Assert.Equal("Active", payload.Activation.State);
        Assert.Equal(payload.SettingsVersion, payload.Activation.ActiveVersion);
        Assert.Equal("NotRequired", payload.Activation.RetryState);
        Assert.NotEmpty(fixture.Context.Response.Headers.ETag.ToString());
        Assert.DoesNotContain("\"ApiKey\":", JsonSerializer.Serialize(payload), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MatchingEtagReturnsNotModified()
    {
        var fixture = CreateFixture();
        var first = await fixture.Endpoint.Get(fixture.CreateRequest("global-admin"));
        var payload = Assert.IsType<GlobalSettingsResponse>(Assert.IsType<ObjectResult>(first).Value);
        var request = fixture.CreateRequest("global-admin");
        request.Headers.IfNoneMatch = $"\"{payload.SettingsVersion}\"";

        var result = await fixture.Endpoint.Get(request);

        var response = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, response.StatusCode);
    }

    [Fact]
    public async Task GlobalAdministratorCanSaveAndReloadNonSecretSettings()
    {
        var fixture = CreateFixture();
        var initial = await fixture.Endpoint.Get(fixture.CreateRequest("global-admin"));
        var initialPayload = Assert.IsType<GlobalSettingsResponse>(Assert.IsType<ObjectResult>(initial).Value);
        var request = fixture.CreateRequest(
            "global-admin",
            new UpdateGlobalSettingsRequest(
                new BrandingSettingsUpdate(
                    "Northwind Support",
                    "NS",
                    null,
                    null,
                    "#135E96",
                    "#006B54",
                    "#006B54",
                    "Support Operations",
                    "support@example.test",
                    null),
                new InvitationSettingsUpdate(
                    "http://localhost:5258/invitations/accept",
                    48),
                new SendGridSettingsUpdate(
                    false,
                    "Northwind Support",
                    "sender@example.test",
                    "support@example.test",
                    ["support@example.test"],
                    "http://localhost:5258",
                    15,
                    4,
                    5,
                    60,
                    "Global",
                    25,
                    60)));
        request.Headers.IfMatch = $"\"{initialPayload.SettingsVersion}\"";
        request.Headers["Idempotency-Key"] = Guid.NewGuid().ToString();

        var result = await fixture.Endpoint.Put(request);

        var response = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<GlobalSettingsResponse>(response.Value);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("Northwind Support", payload.Branding.ProductName);
        Assert.DoesNotContain("\"ApiKey\":", JsonSerializer.Serialize(payload), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Northwind Support", fixture.Store.GetDeploymentSettings()!.ProductName);
    }

    private static Fixture CreateFixture()
    {
        var store = new InMemoryPortalStore();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["Portal:DevelopmentIdentitiesEnabled"] = "true",
                ["Portal:InvitationAcceptanceBaseUrl"] = "http://localhost:5258/invitations/accept",
                ["Portal:InvitationLifetimeHours"] = "72",
                ["Branding:ProductName"] = "Support Portal",
                ["Branding:ShortProductName"] = "SP",
                ["Branding:PrimaryColor"] = "#135E96",
                ["Branding:AccentColor"] = "#006B54",
                ["Branding:FocusColor"] = "#006B54",
                ["Branding:SupportContactName"] = "Support Operations",
                ["Branding:SupportContactEmail"] = "support@example.test",
                ["SendGrid:Enabled"] = "false",
                ["SendGrid:SenderDisplayName"] = "Support Portal",
                ["SendGrid:SenderAddress"] = "sender@example.test",
                ["SendGrid:ReplyToAddress"] = "support@example.test",
                ["SendGrid:PublicPortalUrl"] = "http://localhost:5258",
                ["SendGrid:DataResidency"] = "Global"
            })
            .Build();
        var azure = new AzureOptions
        {
            AuthenticationMode = "Development",
            DevelopmentIdentitiesEnabled = true,
            InvitationAcceptanceBaseUrl = "http://localhost:5258/invitations/accept",
            InvitationLifetimeHours = 72,
            AllowedOrigins = []
        };
        var branding = new BrandingOptions
        {
            ProductName = "Support Portal",
            ShortProductName = "SP",
            PrimaryColor = "#135E96",
            AccentColor = "#006B54",
            FocusColor = "#006B54",
            SupportContactName = "Support Operations",
            SupportContactEmail = "support@example.test"
        };
        var sendGrid = new SendGridOptions
        {
            Enabled = false,
            SenderDisplayName = "Support Portal",
            SenderAddress = "sender@example.test",
            ReplyToAddress = "support@example.test",
            GlobalSupportRecipients = ["support@example.test"],
            PublicPortalUrl = "http://localhost:5258"
        };
        var secrets = new ConfigurationProtectedSecretStore(configuration, "support-portal-sendgrid-api-key");
        var validator = new SettingsCandidateValidator("Development");
        var loader = new SettingsSnapshotLoader(store, secrets, azure, branding, sendGrid, configuration, validator);
        var state = new RuntimeSettingsState(loader.CreateHostDefaults());
        var coordinator = new SettingsRefreshCoordinator(loader, state, TimeProvider.System);
        var service = new GlobalSettingsService(store, secrets, state, coordinator, validator, TimeProvider.System);
        var identity = new EntraClaimsPrincipalFactory(configuration, store);
        var endpoint = new GlobalSettingsEndpoint(identity, service);
        return new Fixture(endpoint, store, configuration);
    }

    private sealed class Fixture(
        GlobalSettingsEndpoint endpoint,
        InMemoryPortalStore store,
        IConfiguration configuration)
    {
        public GlobalSettingsEndpoint Endpoint { get; } = endpoint;
        public InMemoryPortalStore Store { get; } = store;
        public IConfiguration Configuration { get; } = configuration;
        public DefaultHttpContext Context { get; private set; } = new();

        public HttpRequest CreateRequest(string identity, object? body = null)
        {
            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(Configuration)
                .AddSingleton(new AzureOptions { AllowedOrigins = [] })
                .BuildServiceProvider();
            Context = new DefaultHttpContext { RequestServices = services };
            Context.Request.Headers["X-Development-Identity"] = identity;
            if (body is not null)
            {
                Context.Request.ContentType = "application/json";
                Context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body)));
            }

            return Context.Request;
        }
    }
}
