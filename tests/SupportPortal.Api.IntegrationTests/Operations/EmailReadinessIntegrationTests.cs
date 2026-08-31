using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Api.Auth;
using SupportPortal.Api.Endpoints;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Notifications;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;
using AppEmailReadinessRequest = SupportPortal.Application.Notifications.EmailReadinessRequest;
using AppEmailReadinessResult = SupportPortal.Application.Notifications.EmailReadinessResult;
using ContractEmailReadinessResult = SupportPortal.Contracts.Operations.EmailReadinessResult;

namespace SupportPortal.Api.IntegrationTests.Operations;

public sealed class EmailReadinessIntegrationTests
{
    [Fact]
    public async Task OnlyGlobalAdministratorCanRunReadiness()
    {
        var fixture = CreateFixture();

        var result = await fixture.Endpoint.Check(fixture.CreateRequest("global-support", new { mode = "Sandbox" }));

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Empty(fixture.Gateway.Requests);
    }

    [Fact]
    public async Task InvalidLiveInputIsRejectedBeforeTheGateway()
    {
        var fixture = CreateFixture();

        var result = await fixture.Endpoint.Check(fixture.CreateRequest("global-admin", new
        {
            mode = "Live",
            testRecipient = "not-an-email",
            confirmLiveSend = false
        }));

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Empty(fixture.Gateway.Requests);
    }

    [Fact]
    public async Task EmptyOrMalformedBodyReturnsBadRequest()
    {
        var fixture = CreateFixture();
        var request = fixture.CreateRequest("global-admin", new { mode = "Sandbox" });
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{ invalid json"));

        var result = await fixture.Endpoint.Check(request);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Empty(fixture.Gateway.Requests);
    }

    [Fact]
    public async Task SandboxReadinessUsesNoRecipientAndDoesNotChangeNotificationWork()
    {
        var fixture = CreateFixture();
        var notificationCount = fixture.Store.GetNotifications().Count;

        var result = await fixture.Endpoint.Check(fixture.CreateRequest("global-admin", new { mode = "Sandbox" }));

        var response = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<ContractEmailReadinessResult>(response.Value);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("Ready", payload.Outcome);
        Assert.Equal("NoEmailSent", payload.DeliveryMeaning);
        Assert.Null(fixture.Gateway.Requests.Single().TestRecipient);
        Assert.Equal(notificationCount, fixture.Store.GetNotifications().Count);
    }

    [Fact]
    public async Task LiveReadinessReturnsNoRecipientEchoAndDoesNotConsumeOutboxWork()
    {
        var fixture = CreateFixture();
        var notificationCount = fixture.Store.GetNotifications().Count;
        const string recipient = "operator-recipient@example.test";

        var result = await fixture.Endpoint.Check(fixture.CreateRequest("global-admin", new
        {
            mode = "Live",
            testRecipient = recipient,
            confirmLiveSend = true
        }));

        var response = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<ContractEmailReadinessResult>(response.Value);
        var serialized = JsonSerializer.Serialize(payload);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("Accepted", payload.Outcome);
        Assert.Equal(202, payload.ProviderHttpStatus);
        Assert.Equal("AcceptedBySendGridMailboxDeliveryUnconfirmed", payload.DeliveryMeaning);
        Assert.DoesNotContain(recipient, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(notificationCount, fixture.Store.GetNotifications().Count);
        Assert.Equal(recipient, fixture.Gateway.Requests.Single().TestRecipient);
    }

    [Fact]
    public async Task DisabledReadinessMapsToUnavailableWithoutProviderDetails()
    {
        var fixture = CreateFixture(new AppEmailReadinessResult(
            EmailReadinessMode.Sandbox,
            EmailReadinessOutcome.Disabled,
            "Configuration",
            null,
            "Disabled",
            DateTimeOffset.UtcNow,
            "correlation",
            "NoProviderRequestMade",
            []));

        var result = await fixture.Endpoint.Check(fixture.CreateRequest("global-admin", new { mode = "Sandbox" }));

        var response = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<ContractEmailReadinessResult>(response.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
        Assert.Equal("Disabled", payload.Outcome);
        Assert.Equal("NoProviderRequestMade", payload.DeliveryMeaning);
        Assert.DoesNotContain("api", JsonSerializer.Serialize(payload), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderUnavailableReadinessReturnsSafeOutcomeWithoutNotificationMutation()
    {
        var fixture = CreateFixture(new AppEmailReadinessResult(
            EmailReadinessMode.Live,
            EmailReadinessOutcome.ProviderUnavailable,
            "SenderAcceptance",
            null,
            "NetworkUnavailable",
            DateTimeOffset.UtcNow,
            "correlation",
            "NoEmailSent",
            []));
        var notificationCount = fixture.Store.GetNotifications().Count;

        var result = await fixture.Endpoint.Check(fixture.CreateRequest("global-admin", new
        {
            mode = "Live",
            testRecipient = "operator-recipient@example.test",
            confirmLiveSend = true
        }));

        var response = Assert.IsType<ObjectResult>(result);
        var payload = Assert.IsType<ContractEmailReadinessResult>(response.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
        Assert.Equal("ProviderUnavailable", payload.Outcome);
        Assert.Equal("NetworkUnavailable", payload.FailureCategory);
        Assert.Equal("NoEmailSent", payload.DeliveryMeaning);
        Assert.Equal(notificationCount, fixture.Store.GetNotifications().Count);
        Assert.DoesNotContain("operator-recipient@example.test", JsonSerializer.Serialize(payload), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadinessAuditContainsSafeOperationMetadataOnly()
    {
        var fixture = CreateFixture();

        await fixture.Endpoint.Check(fixture.CreateRequest("global-admin", new { mode = "Sandbox" }));

        var audit = Assert.Single(fixture.Store.GetAuditEvents(), item => item.EventType == "EmailReadinessChecked");
        Assert.True(audit.Succeeded);
        Assert.Contains("EmailReadinessChecked", audit.Metadata, StringComparison.Ordinal);
        Assert.Contains("Sandbox", audit.Metadata, StringComparison.Ordinal);
        Assert.Contains("NoEmailSent", audit.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain("operator-recipient@example.test", audit.Metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", audit.Metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider-body", audit.Metadata, StringComparison.OrdinalIgnoreCase);
    }

    private static Fixture CreateFixture(AppEmailReadinessResult? response = null)
    {
        var store = new InMemoryPortalStore();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["Portal:DevelopmentIdentitiesEnabled"] = "true"
            })
            .Build();
        var gateway = new FakeReadinessGateway(response);
        var identityFactory = new EntraClaimsPrincipalFactory(configuration, store);
        var endpoint = new EmailReadinessEndpoint(identityFactory, new EmailReadinessService(gateway, store: store));
        return new Fixture(endpoint, store, gateway, configuration);
    }

    private static DefaultHttpContext CreateRequest(string identity, object body, IConfiguration? configuration = null)
    {
        configuration ??= new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["Portal:DevelopmentIdentitiesEnabled"] = "true"
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(new AzureOptions { AllowedOrigins = [] })
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers["X-Development-Identity"] = identity;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body)));
        return context;
    }

    private sealed record Fixture(
        EmailReadinessEndpoint Endpoint,
        InMemoryPortalStore Store,
        FakeReadinessGateway Gateway,
        IConfiguration Configuration)
    {
        public HttpRequest CreateRequest(string identity, object body) =>
            EmailReadinessIntegrationTests.CreateRequest(identity, body, Configuration).Request;
    }

    private sealed class FakeReadinessGateway(AppEmailReadinessResult? configuredResponse = null) : IEmailReadinessGateway
    {
        public List<AppEmailReadinessRequest> Requests { get; } = [];

        public Task<AppEmailReadinessResult> CheckAsync(AppEmailReadinessRequest request, string correlationId, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(configuredResponse ?? (request.Mode == EmailReadinessMode.Sandbox
                ? new AppEmailReadinessResult(request.Mode, EmailReadinessOutcome.Ready, "PayloadValidation", 200, "None", DateTimeOffset.UtcNow, correlationId, "NoEmailSent", [])
                : new AppEmailReadinessResult(request.Mode, EmailReadinessOutcome.Accepted, "SenderAcceptance", 202, "None", DateTimeOffset.UtcNow, correlationId, "AcceptedBySendGridMailboxDeliveryUnconfirmed", [])));
        }
    }
}
