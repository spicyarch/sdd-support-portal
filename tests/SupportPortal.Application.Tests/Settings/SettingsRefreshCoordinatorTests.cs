using SupportPortal.Application.Branding;
using SupportPortal.Application.Settings;
using SupportPortal.Domain.Settings;

namespace SupportPortal.Application.Tests.Settings;

public sealed class SettingsRefreshCoordinatorTests
{
    [Fact]
    public async Task TwoInstancesConvergeOnTheSharedRevisionAtTheThirtySecondPollBoundary()
    {
        var now = DateTimeOffset.Parse("2026-08-24T00:00:00Z");
        var clock = new MutableTimeProvider(now);
        var loader = new SharedSnapshotLoader
        {
            DesiredVersion = "revision-2"
        };
        loader.Snapshots["revision-2"] = CreateSnapshot("revision-2", now);
        var first = new RuntimeSettingsState(CreateSnapshot("revision-1", now));
        var second = new RuntimeSettingsState(CreateSnapshot("revision-1", now));
        var firstCoordinator = new SettingsRefreshCoordinator(loader, first, clock);
        var secondCoordinator = new SettingsRefreshCoordinator(loader, second, clock);

        Assert.True(await firstCoordinator.RefreshIfDueAsync());
        Assert.True(await secondCoordinator.RefreshIfDueAsync());
        Assert.Equal("revision-2", first.Current.Version);
        Assert.Equal("revision-2", second.Current.Version);

        clock.Advance(TimeSpan.FromSeconds(29));
        loader.DesiredVersion = "revision-3";
        loader.Snapshots["revision-3"] = CreateSnapshot("revision-3", clock.GetUtcNow());
        Assert.False(await firstCoordinator.RefreshIfDueAsync());

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await firstCoordinator.RefreshIfDueAsync());
        Assert.Equal("revision-3", first.Current.Version);
    }

    [Fact]
    public async Task ImmediateRefreshPublishesARevisionWithoutWaitingForThePollInterval()
    {
        var now = DateTimeOffset.UtcNow;
        var loader = new SharedSnapshotLoader
        {
            DesiredVersion = "revision-2"
        };
        loader.Snapshots["revision-2"] = CreateSnapshot("revision-2", now);
        var state = new RuntimeSettingsState(CreateSnapshot("revision-1", now));
        var coordinator = new SettingsRefreshCoordinator(loader, state, new MutableTimeProvider(now));

        Assert.True(await coordinator.RefreshNowAsync());
        Assert.Equal("revision-2", state.Current.Version);
        Assert.Equal(SettingsActivationState.Active, state.Activation.State);
        Assert.Equal("revision-2", state.Activation.ActiveVersion);
    }

    [Fact]
    public async Task InterruptedRefreshRetainsThePriorSnapshotAndRecoversOnTheNextAttempt()
    {
        var now = DateTimeOffset.UtcNow;
        var loader = new SharedSnapshotLoader
        {
            DesiredVersion = "revision-2",
            CancelNextLoad = true
        };
        loader.Snapshots["revision-2"] = CreateSnapshot("revision-2", now);
        var state = new RuntimeSettingsState(CreateSnapshot("revision-1", now));
        var coordinator = new SettingsRefreshCoordinator(loader, state, new MutableTimeProvider(now));

        Assert.False(await coordinator.RefreshNowAsync());
        Assert.Equal("revision-1", state.Current.Version);
        Assert.Equal(SettingsActivationState.ActivationFailed, state.Activation.State);
        Assert.Equal("revision-1", state.Activation.ActiveVersion);
        Assert.Equal("revision-2", state.Activation.DesiredVersion);

        Assert.True(await coordinator.RefreshNowAsync());
        Assert.Equal("revision-2", state.Current.Version);
        Assert.Equal(SettingsActivationState.Active, state.Activation.State);
        Assert.Null(state.Activation.FailureCategory);
    }

    private static EffectiveSettingsSnapshot CreateSnapshot(string version, DateTimeOffset loadedAt)
    {
        var brand = BrandingResolver.Resolve(new BrandingInput(
            "Support Portal",
            "SP",
            null,
            null,
            null,
            null,
            null,
            "Support",
            "support@example.test",
            null), "Development");
        return new EffectiveSettingsSnapshot(
            version,
            SettingsSource.AdministratorOverride,
            brand,
            "http://localhost:5258/invitations/accept",
            72,
            new EffectiveSendGridSettings(
                true,
                "runtime-key",
                "Support Portal",
                "sender@example.test",
                "support@example.test",
                ["global-support@example.test"],
                "http://localhost:5258",
                15,
                4,
                5,
                60,
                "Global",
                25,
                60),
            new RuntimeEmailAvailability(RuntimeEmailAvailabilityState.Ready, [], loadedAt),
            true,
            SettingsApiKeyMode.Managed,
            loadedAt);
    }

    private sealed class SharedSnapshotLoader : ISettingsSnapshotLoader
    {
        public string? DesiredVersion { get; set; }

        public bool CancelNextLoad { get; set; }

        public Dictionary<string, EffectiveSettingsSnapshot> Snapshots { get; } = [];

        public Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DesiredVersion);
        }

        public Task<EffectiveSettingsSnapshot> LoadAsync(string version, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CancelNextLoad)
            {
                CancelNextLoad = false;
                throw new OperationCanceledException();
            }

            return Task.FromResult(Snapshots[version]);
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public DateTimeOffset Current { get; private set; } = current;

        public override DateTimeOffset GetUtcNow() => Current;

        public void Advance(TimeSpan duration) => Current = Current.Add(duration);
    }
}
