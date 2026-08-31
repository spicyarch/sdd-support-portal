using Microsoft.Extensions.Configuration;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Common;
using SupportPortal.Application.Settings;
using SupportPortal.Contracts.Settings;
using SupportPortal.Domain.Authorization;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Observability;

public sealed class SettingsOperationsObservabilityTests
{
    [Fact]
    public async Task RejectedSaveAuditsOnlySafeSettingNames()
    {
        var fixture = CreateFixture();
        var submittedValue = new string('x', 101);
        var request = ValidRequest() with
        {
            Branding = new BrandingSettingsUpdate(submittedValue, null, null, null, null, null, null, null, null, null)
        };

        var exception = await Assert.ThrowsAsync<PortalServiceException>(() => fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            "host-defaults",
            Guid.NewGuid(),
            request,
            "correlation"));

        var audit = Assert.Single(fixture.Store.GetAuditEvents(), item => item.EventType == SettingsAuditPolicy.SettingsSaveRejected);
        Assert.Equal(400, exception.StatusCode);
        Assert.False(audit.Succeeded);
        Assert.Contains("Branding:ProductName", audit.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain(submittedValue, audit.Metadata, StringComparison.Ordinal);
        Assert.Null(fixture.Store.GetDeploymentSettings());
    }

    [Fact]
    public async Task KeyReplacementAndClearUseSafeDedicatedAuditEvents()
    {
        var fixture = CreateFixture();
        var replacement = await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            "host-defaults",
            Guid.NewGuid(),
            ValidRequest(apiKey: "replacement-secret", enabled: true),
            "replace");
        var cleared = await fixture.Service.ReplaceAsync(
            GlobalAdministrator(),
            replacement.SettingsVersion,
            Guid.NewGuid(),
            ValidRequest(clearApiKey: true, enabled: false),
            "clear");

        var audits = fixture.Store.GetAuditEvents()
            .Where(item => item.EventType is SettingsAuditPolicy.ApiKeyReplaced or SettingsAuditPolicy.ApiKeyCleared)
            .ToArray();
        Assert.Equal(2, audits.Length);
        Assert.Contains(audits, item => item.EventType == SettingsAuditPolicy.ApiKeyReplaced);
        Assert.Contains(audits, item => item.EventType == SettingsAuditPolicy.ApiKeyCleared);
        Assert.All(audits, item =>
        {
            Assert.True(item.Succeeded);
            Assert.Contains("SendGrid:ApiKey", item.Metadata, StringComparison.Ordinal);
            Assert.DoesNotContain("replacement-secret", item.Metadata, StringComparison.Ordinal);
        });
        Assert.False(cleared.SendGrid.ApiKeyConfigured);
    }

    [Fact]
    public async Task ActivationFailureKeepsTheLastKnownGoodRevisionAndSafeRetryState()
    {
        var fixture = CreateFixture();
        var now = DateTimeOffset.UtcNow;
        fixture.Runtime.BeginRefresh("desired-revision", now);
        fixture.Runtime.Fail("desired-revision", now, "InvalidConfiguration", ["SendGrid:ApiKey"]);

        var response = await fixture.Service.GetAsync(GlobalAdministrator());

        Assert.Equal("host-defaults", response.SettingsVersion);
        Assert.Equal("ActivationFailed", response.Activation.State);
        Assert.Equal("host-defaults", response.Activation.ActiveVersion);
        Assert.Equal("desired-revision", response.Activation.DesiredVersion);
        Assert.Equal("Scheduled", response.Activation.RetryState);
        Assert.Equal(["SendGrid:ApiKey"], response.Activation.InvalidSettingNames);
        Assert.DoesNotContain("secret", System.Text.Json.JsonSerializer.Serialize(response), StringComparison.OrdinalIgnoreCase);
    }

    private static UpdateGlobalSettingsRequest ValidRequest(
        string? apiKey = null,
        bool clearApiKey = false,
        bool enabled = false) => new(
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

    private static PortalPrincipal GlobalAdministrator()
    {
        var identity = DevelopmentIdentities.All.Single(item => item.Key == "global-admin");
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
        return new Fixture(service, store, runtime);
    }

    private sealed record Fixture(GlobalSettingsService Service, InMemoryPortalStore Store, RuntimeSettingsState Runtime);

    private sealed class FakeProtectedSecretStore : IProtectedSecretStore
    {
        public string? Value { get; private set; }

        public Task<string?> GetAsync(string? version, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Value);
        }

        public Task<ProtectedSecretReference> SetAsync(string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Value = value;
            return Task.FromResult(new ProtectedSecretReference("version-1"));
        }
    }
}
