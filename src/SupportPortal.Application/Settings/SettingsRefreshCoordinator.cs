namespace SupportPortal.Application.Settings;

public sealed class SettingsRefreshException : Exception
{
    public SettingsRefreshException(
        string category,
        IReadOnlyList<string>? invalidSettingNames = null,
        Exception? innerException = null)
        : base(category, innerException)
    {
        Category = category;
        InvalidSettingNames = invalidSettingNames ?? [];
    }

    public string Category { get; }

    public IReadOnlyList<string> InvalidSettingNames { get; }
}

public interface ISettingsSnapshotLoader
{
    Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken);

    Task<EffectiveSettingsSnapshot> LoadAsync(string version, CancellationToken cancellationToken);
}

public sealed class SettingsRefreshCoordinator
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    private readonly ISettingsSnapshotLoader loader;
    private readonly RuntimeSettingsState state;
    private readonly TimeProvider clock;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly object scheduleLock = new();
    private DateTimeOffset nextCheckAt = DateTimeOffset.MinValue;

    public SettingsRefreshCoordinator(
        ISettingsSnapshotLoader loader,
        RuntimeSettingsState state,
        TimeProvider clock)
    {
        this.loader = loader;
        this.state = state;
        this.clock = clock;
    }

    public RuntimeSettingsState State => state;

    public async Task<bool> RefreshIfDueAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        lock (scheduleLock)
        {
            if (now < nextCheckAt)
            {
                return false;
            }

            nextCheckAt = now.Add(RefreshInterval);
        }

        return await RefreshNowAsync(cancellationToken);
    }

    public async Task<bool> RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        if (!await refreshGate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        try
        {
            var desiredVersion = await loader.GetCurrentVersionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(desiredVersion) ||
                StringComparer.Ordinal.Equals(desiredVersion, state.Current.Version))
            {
                return false;
            }

            var attemptedAt = clock.GetUtcNow();
            state.BeginRefresh(desiredVersion, attemptedAt);
            try
            {
                var snapshot = await loader.LoadAsync(desiredVersion, cancellationToken);
                state.Publish(snapshot, clock.GetUtcNow());
                return true;
            }
            catch (SettingsRefreshException exception)
            {
                state.Fail(desiredVersion, clock.GetUtcNow(), exception.Category, exception.InvalidSettingNames);
                return false;
            }
            catch (Exception)
            {
                state.Fail(desiredVersion, clock.GetUtcNow(), "SettingsRefreshFailed");
                return false;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            state.Fail(state.Current.Version, clock.GetUtcNow(), "SettingsStoreUnavailable");
            return false;
        }
        finally
        {
            refreshGate.Release();
        }
    }
}
