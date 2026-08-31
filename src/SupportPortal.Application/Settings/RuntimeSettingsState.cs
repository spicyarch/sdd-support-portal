namespace SupportPortal.Application.Settings;

public enum SettingsActivationState
{
    Active,
    Refreshing,
    ActivationFailed
}

public sealed record SettingsActivationStatus(
    SettingsActivationState State,
    string ActiveVersion,
    string DesiredVersion,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastSuccessfulAt,
    string? FailureCategory,
    IReadOnlyList<string> InvalidSettingNames);

public sealed class RuntimeSettingsState
{
    private EffectiveSettingsSnapshot current;
    private SettingsActivationStatus activation;

    public RuntimeSettingsState(EffectiveSettingsSnapshot initial)
    {
        current = initial ?? throw new ArgumentNullException(nameof(initial));
        activation = new SettingsActivationStatus(
            SettingsActivationState.Active,
            initial.Version,
            initial.Version,
            null,
            initial.LoadedAt,
            null,
            []);
    }

    public EffectiveSettingsSnapshot Current => Volatile.Read(ref current);

    public SettingsActivationStatus Activation => Volatile.Read(ref activation);

    public void BeginRefresh(string desiredVersion, DateTimeOffset attemptedAt)
    {
        var snapshot = Current;
        Interlocked.Exchange(
            ref activation,
            new SettingsActivationStatus(
                SettingsActivationState.Refreshing,
                snapshot.Version,
                desiredVersion,
                attemptedAt,
                Activation.LastSuccessfulAt,
                null,
                []));
    }

    public void Publish(EffectiveSettingsSnapshot snapshot, DateTimeOffset publishedAt)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Interlocked.Exchange(ref current, snapshot);
        Interlocked.Exchange(
            ref activation,
            new SettingsActivationStatus(
                SettingsActivationState.Active,
                snapshot.Version,
                snapshot.Version,
                Activation.LastAttemptAt,
                publishedAt,
                null,
                []));
    }

    public void Fail(string desiredVersion, DateTimeOffset attemptedAt, string failureCategory, IReadOnlyList<string>? invalidSettingNames = null)
    {
        var snapshot = Current;
        Interlocked.Exchange(
            ref activation,
            new SettingsActivationStatus(
                SettingsActivationState.ActivationFailed,
                snapshot.Version,
                desiredVersion,
                attemptedAt,
                Activation.LastSuccessfulAt,
                failureCategory,
                invalidSettingNames ?? []));
    }
}
