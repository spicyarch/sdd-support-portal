using Microsoft.EntityFrameworkCore;
using SupportPortal.Application.Settings;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Settings;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Settings;

public sealed class SettingsActivationIntegrationTests
{
    [Fact]
    public async Task IndependentStoreContextsObserveTheSharedRevision()
    {
        var options = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        SeedSettings(options, now, "revision-2");
        using var firstContext = new SupportPortalDbContext(options);
        using var secondContext = new SupportPortalDbContext(options);
        var firstLoader = new EfRevisionSnapshotLoader(new EfPortalStore(firstContext));
        var secondLoader = new EfRevisionSnapshotLoader(new EfPortalStore(secondContext));
        var firstState = new RuntimeSettingsState(CreateSnapshot("revision-1", now));
        var secondState = new RuntimeSettingsState(CreateSnapshot("revision-1", now));
        var firstCoordinator = new SettingsRefreshCoordinator(firstLoader, firstState, new FixedTimeProvider(now));
        var secondCoordinator = new SettingsRefreshCoordinator(secondLoader, secondState, new FixedTimeProvider(now));

        Assert.True(await firstCoordinator.RefreshIfDueAsync());
        Assert.True(await secondCoordinator.RefreshIfDueAsync());
        Assert.Equal("revision-2", firstState.Current.Version);
        Assert.Equal("revision-2", secondState.Current.Version);
        Assert.Equal(SettingsActivationState.Active, firstState.Activation.State);
        Assert.Equal(SettingsActivationState.Active, secondState.Activation.State);
    }

    [Fact]
    public void NewStoreContextRetainsTheSavedRevisionAndRecipients()
    {
        var options = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        SeedSettings(options, now, "revision-persisted");

        using var restartedContext = new SupportPortalDbContext(options);
        var store = new EfPortalStore(restartedContext);
        var settings = Assert.IsType<DeploymentSettings>(store.GetDeploymentSettings());
        var recipients = store.GetDeploymentSettingsRecipients(settings.DeploymentSettingsId);

        Assert.Equal("revision-persisted", settings.Revision);
        Assert.Single(recipients);
        Assert.Equal("support@example.test", recipients[0].NormalizedAddress);
    }

    private static DbContextOptions<SupportPortalDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<SupportPortalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static void SeedSettings(
        DbContextOptions<SupportPortalDbContext> options,
        DateTimeOffset now,
        string revision)
    {
        using var context = new SupportPortalDbContext(options);
        context.Database.EnsureCreated();
        var store = new EfPortalStore(context);
        var userId = Guid.NewGuid();
        var settingsId = Guid.NewGuid();
        store.Execute(() =>
        {
            store.AddUser(new User(userId, Guid.NewGuid(), Guid.NewGuid(), "Global Administrator", "admin@example.test", now));
            store.AddDeploymentSettings(new DeploymentSettings(
                settingsId,
                now,
                userId,
                revision,
                new DeploymentSettingsValues(
                    "Support Portal",
                    "SP",
                    null,
                    null,
                    "#135E96",
                    "#006B54",
                    "#006B54",
                    "Support Operations",
                    "support@example.test",
                    null,
                    "https://portal.example.test/invitations/accept",
                    72,
                    true,
                    "Support Portal",
                    "sender@example.test",
                    "support@example.test",
                    "https://portal.example.test",
                    15,
                    4,
                    5,
                    60,
                    "Global",
                    25,
                    60,
                    SettingsApiKeyMode.Cleared,
                    null)));
            store.AddDeploymentSettingsRecipient(new DeploymentSettingsRecipient(
                Guid.NewGuid(),
                settingsId,
                "support@example.test",
                now));
        });
    }

    private static EffectiveSettingsSnapshot CreateSnapshot(string version, DateTimeOffset loadedAt) =>
        new(
            version,
            SettingsSource.AdministratorOverride,
            new SupportPortal.Application.Branding.EffectiveBrandProfile(
                "Support Portal",
                "SP",
                "SP",
                null,
                null,
                "#135E96",
                "#006B54",
                "#006B54",
                "Support Operations",
                "support@example.test",
                null,
                version),
            "https://portal.example.test/invitations/accept",
            72,
            new EffectiveSendGridSettings(
                false,
                null,
                "Support Portal",
                "sender@example.test",
                "support@example.test",
                ["support@example.test"],
                "https://portal.example.test",
                15,
                4,
                5,
                60,
                "Global",
                25,
                60),
            new RuntimeEmailAvailability(RuntimeEmailAvailabilityState.Disabled, [], loadedAt),
            false,
            SettingsApiKeyMode.Cleared,
            loadedAt);

    private sealed class EfRevisionSnapshotLoader(EfPortalStore store) : ISettingsSnapshotLoader
    {
        public Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(store.GetDeploymentSettings()?.Revision);
        }

        public Task<EffectiveSettingsSnapshot> LoadAsync(string version, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateSnapshot(version, DateTimeOffset.UtcNow));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
