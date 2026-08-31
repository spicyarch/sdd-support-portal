using Microsoft.Extensions.Configuration;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Common;
using SupportPortal.Application.Settings;
using SupportPortal.Contracts.Settings;
using SupportPortal.Domain.Settings;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Settings;

public sealed class GlobalSettingsSecurityTests
{
    [Fact]
    public async Task StaleRevisionIsRejectedBeforeSecretStaging()
    {
        var fixture = CreateFixture();
        var first = await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            "host-defaults",
            Guid.NewGuid(),
            ValidRequest("first-secret"),
            "first");

        var exception = await Assert.ThrowsAsync<PortalServiceException>(() =>
            fixture.Service.ReplaceAsync(
                GlobalAdministrator(),
                "host-defaults",
                Guid.NewGuid(),
                ValidRequest("second-secret"),
                "stale"));

        Assert.Equal(412, exception.StatusCode);
        Assert.Equal("first-secret", fixture.Secrets.Value);
        Assert.Equal(1, fixture.Secrets.SetCalls);
        Assert.Equal(first.SettingsVersion, fixture.Store.GetDeploymentSettings()!.Revision);
    }

    [Fact]
    public async Task IdempotentRetryDoesNotStageTheSecretTwice()
    {
        var fixture = CreateFixture();
        var idempotencyKey = Guid.NewGuid();
        var request = ValidRequest("same-secret");

        var first = await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            "host-defaults",
            idempotencyKey,
            request,
            "first");
        var replay = await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            "host-defaults",
            idempotencyKey,
            request,
            "replay");

        Assert.Equal(first.SettingsVersion, replay.SettingsVersion);
        Assert.Equal(1, fixture.Secrets.SetCalls);
        Assert.Single(fixture.Store.GetDeploymentSettingsRecipients(fixture.Store.GetDeploymentSettings()!.DeploymentSettingsId));
    }

    [Fact]
    public async Task ProtectedSecretFailureLeavesTheSettingsStoreUnchanged()
    {
        var fixture = CreateFixture(secretFailure: true);

        var exception = await Assert.ThrowsAsync<PortalServiceException>(() =>
            fixture.Service.ReplaceAsync(
                GlobalAdministrator(),
                "host-defaults",
                Guid.NewGuid(),
                ValidRequest("secret-value"),
                "secret-failure"));

        Assert.Equal(503, exception.StatusCode);
        Assert.Null(fixture.Store.GetDeploymentSettings());
        Assert.Equal(1, fixture.Secrets.SetCalls);
    }

    private static UpdateGlobalSettingsRequest ValidRequest(string? apiKey = null) => new(
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
        new InvitationSettingsUpdate("http://localhost:5258/invitations/accept", 72),
        new SendGridSettingsUpdate(
            true,
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
            60,
            apiKey));

    private static PortalPrincipal GlobalAdministrator()
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == "global-admin");
        return new PortalPrincipal(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Role, identity.TeamId, true);
    }

    private static Fixture CreateFixture(bool secretFailure = false)
    {
        var store = new InMemoryPortalStore();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
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
                ["SendGrid:PublicPortalUrl"] = "http://localhost:5258"
            })
            .Build();
        var azure = new AzureOptions
        {
            AuthenticationMode = "Development",
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
        var secrets = new FakeProtectedSecretStore(secretFailure);
        var validator = new SettingsCandidateValidator("Development");
        var loader = new SettingsSnapshotLoader(store, secrets, azure, branding, sendGrid, configuration, validator);
        var runtime = new RuntimeSettingsState(loader.CreateHostDefaults());
        var coordinator = new SettingsRefreshCoordinator(loader, runtime, TimeProvider.System);
        var service = new GlobalSettingsService(store, secrets, runtime, coordinator, validator, TimeProvider.System);
        return new Fixture(service, store, secrets);
    }

    private sealed record Fixture(GlobalSettingsService Service, InMemoryPortalStore Store, FakeProtectedSecretStore Secrets);

    private sealed class FakeProtectedSecretStore(bool failSet) : IProtectedSecretStore
    {
        public string? Value { get; private set; }
        public int SetCalls { get; private set; }

        public Task<string?> GetAsync(string? version, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Value);
        }

        public Task<ProtectedSecretReference> SetAsync(string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetCalls++;
            if (failSet)
            {
                throw new ProtectedSecretStoreException("Unavailable");
            }

            Value = value;
            return Task.FromResult(new ProtectedSecretReference($"version-{SetCalls}"));
        }
    }
}
