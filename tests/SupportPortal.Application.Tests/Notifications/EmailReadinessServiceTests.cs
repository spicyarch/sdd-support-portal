using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Common;
using SupportPortal.Application.Notifications;
using SupportPortal.Domain.Authorization;
using AppEmailReadinessRequest = SupportPortal.Application.Notifications.EmailReadinessRequest;
using AppEmailReadinessResult = SupportPortal.Application.Notifications.EmailReadinessResult;
using ContractEmailReadinessRequest = SupportPortal.Contracts.Operations.EmailReadinessRequest;

namespace SupportPortal.Application.Tests.Notifications;

public sealed class EmailReadinessServiceTests
{
    [Fact]
    public async Task OnlyGlobalAdministratorCanRunReadiness()
    {
        var gateway = new FakeReadinessGateway();
        var service = new EmailReadinessService(gateway);

        var exception = await Assert.ThrowsAsync<PortalServiceException>(() => service.CheckAsync(
            Principal("global-support"),
            new ContractEmailReadinessRequest("Sandbox"),
            "correlation",
            CancellationToken.None));

        Assert.Equal(403, exception.StatusCode);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task LiveReadinessRequiresExplicitConfirmationAndValidRecipient()
    {
        var service = new EmailReadinessService(new FakeReadinessGateway());

        var exception = await Assert.ThrowsAsync<PortalServiceException>(() => service.CheckAsync(
            Principal("global-admin"),
            new ContractEmailReadinessRequest("Live", "not-an-email", false),
            "correlation",
            CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task SandboxReadinessDelegatesWithoutRecipient()
    {
        var gateway = new FakeReadinessGateway();
        var service = new EmailReadinessService(gateway);

        var result = await service.CheckAsync(
            Principal("global-admin"),
            new ContractEmailReadinessRequest("Sandbox"),
            "correlation",
            CancellationToken.None);

        Assert.Equal(EmailReadinessMode.Sandbox, gateway.Requests.Single().Mode);
        Assert.Null(gateway.Requests.Single().TestRecipient);
        Assert.Equal("correlation", result.CorrelationId);
    }

    private static PortalPrincipal Principal(string key)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Role, identity.TeamId, true);
    }

    private sealed class FakeReadinessGateway : IEmailReadinessGateway
    {
        public List<AppEmailReadinessRequest> Requests { get; } = [];

        public Task<AppEmailReadinessResult> CheckAsync(AppEmailReadinessRequest request, string correlationId, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new AppEmailReadinessResult(
                request.Mode,
                EmailReadinessOutcome.Ready,
                "PayloadValidation",
                200,
                "None",
                DateTimeOffset.UtcNow,
                correlationId,
                "NoEmailSent",
                []));
        }
    }
}