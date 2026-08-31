using System.Text.Json;
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

namespace SupportPortal.Application.Tests.Settings;

public sealed class GlobalSettingsServiceTests
{
    [Fact]
    public async Task ReplacingApiKeyStoresOnlyProtectedReference()
    {
        var fixture = CreateFixture();

        var response = await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            "host-defaults",
            Guid.NewGuid(),
            ValidRequest(apiKey: "new-secret"),
            "correlation");

        var persisted = fixture.Store.GetDeploymentSettings();
        var serialized = JsonSerializer.Serialize(response);
        Assert.NotNull(persisted);
        Assert.Equal(SettingsApiKeyMode.Managed, persisted!.SendGridApiKeyMode);
        Assert.NotEqual("new-secret", persisted.SendGridApiKeySecretVersion);
        Assert.Equal("new-secret", fixture.Secrets.Value);
        Assert.Equal(1, fixture.Secrets.SetCalls);
        Assert.DoesNotContain("new-secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ApiKey\":", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BlankApiKeyPreservesTheExistingProtectedSecret()
    {
        var fixture = CreateFixture();
        var first = await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            "host-defaults",
            Guid.NewGuid(),
            ValidRequest(apiKey: "existing-secret"),
            "first");

        await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            first.SettingsVersion,
            Guid.NewGuid(),
            ValidRequest(),
            "second");

        Assert.Equal("existing-secret", fixture.Secrets.Value);
        Assert.Equal(1, fixture.Secrets.SetCalls);
        Assert.Equal(SettingsApiKeyMode.Managed, fixture.Store.GetDeploymentSettings()!.SendGridApiKeyMode);
    }

    [Fact]
    public async Task ExplicitClearDisablesTheProtectedSecret()
    {
        var fixture = CreateFixture();
        var first = await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            "host-defaults",
            Guid.NewGuid(),
            ValidRequest(apiKey: "existing-secret"),
            "first");

        var cleared = await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            first.SettingsVersion,
            Guid.NewGuid(),
            ValidRequest(clearApiKey: true, enabled: false),
            "clear");

        Assert.Equal(SettingsApiKeyMode.Cleared, fixture.Store.GetDeploymentSettings()!.SendGridApiKeyMode);
        Assert.False(cleared.SendGrid.ApiKeyConfigured);
        Assert.Equal(1, fixture.Secrets.SetCalls);
    }

    [Fact]
    public async Task InvalidSaveLeavesTheExistingSettingsAndSecretUnchanged()
    {
        var fixture = CreateFixture();
        var first = await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            "host-defaults",
            Guid.NewGuid(),
            ValidRequest(apiKey: "existing-secret"),
            "first");
        var invalid = ValidRequest(apiKey: "replacement-secret") with
        {
            Branding = new BrandingSettingsUpdate(new string('x', 101), null, null, null, null, null, null, null, null, null)
        };

        var exception = await Assert.ThrowsAsync<PortalServiceException>(() =>
            fixture.Service.ReplaceAsync(
                GlobalAdministrator(),
                first.SettingsVersion,
                Guid.NewGuid(),
                invalid,
                "invalid"));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(first.SettingsVersion, fixture.Store.GetDeploymentSettings()!.Revision);
        Assert.Equal("existing-secret", fixture.Secrets.Value);
        Assert.Equal(1, fixture.Secrets.SetCalls);
    }

    [Fact]
    public async Task NonGlobalAdministratorCannotReadSettings()
    {
        var fixture = CreateFixture();

        var exception = await Assert.ThrowsAsync<PortalServiceException>(() =>
            fixture.Service.GetAsync(Principal("global-support")));

        Assert.Equal(403, exception.StatusCode);
    }

    private static UpdateGlobalSettingsRequest ValidRequest(string? apiKey = null, bool clearApiKey = false, bool enabled = true) => new(
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
            72),
        new SendGridSettingsUpdate(
            enabled,
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
            apiKey,
            clearApiKey));

    private static PortalPrincipal GlobalAdministrator() => Principal("global-admin");

    private static PortalPrincipal Principal(string key)
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == key);
        return new PortalPrincipal(
            identity.UserId,
            DevelopmentIdentities.TenantId,
            identity.ObjectId,
            identity.DisplayName,
            identity.Role,
            identity.TeamId,
            true);
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
                ["SendGrid:GlobalSupportRecipients:0"] = "support@example.test",
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
        var secrets = new FakeProtectedSecretStore();
        var validator = new SettingsCandidateValidator("Development");
        var loader = new SettingsSnapshotLoader(store, secrets, azure, branding, sendGrid, configuration, validator);
        var runtime = new RuntimeSettingsState(loader.CreateHostDefaults());
        var coordinator = new SettingsRefreshCoordinator(loader, runtime, TimeProvider.System);
        var service = new GlobalSettingsService(store, secrets, runtime, coordinator, validator, TimeProvider.System);
        return new Fixture(service, store, secrets);
    }

    private sealed record Fixture(
        GlobalSettingsService Service,
        InMemoryPortalStore Store,
        FakeProtectedSecretStore Secrets);

    private sealed class FakeProtectedSecretStore : IProtectedSecretStore
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
            Value = value;
            SetCalls++;
            return Task.FromResult(new ProtectedSecretReference($"version-{SetCalls}"));
        }
    }
}
