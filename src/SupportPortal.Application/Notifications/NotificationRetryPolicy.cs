namespace SupportPortal.Application.Notifications;

using SupportPortal.Application.Settings;

public sealed class NotificationRetryPolicy
{
    private readonly int maximumAttempts;
    private readonly TimeSpan minimumBackoff;
    private readonly TimeSpan maximumBackoff;
    private readonly Random random;
    private readonly RuntimeSettingsState? runtimeSettings;

    public NotificationRetryPolicy(
        int maximumAttempts,
        TimeSpan minimumBackoff,
        TimeSpan maximumBackoff,
        Random? random = null,
        RuntimeSettingsState? runtimeSettings = null)
    {
        this.maximumAttempts = Math.Clamp(maximumAttempts, 1, 10);
        this.minimumBackoff = minimumBackoff > TimeSpan.Zero ? minimumBackoff : TimeSpan.FromSeconds(1);
        this.maximumBackoff = maximumBackoff >= this.minimumBackoff ? maximumBackoff : this.minimumBackoff;
        this.random = random ?? Random.Shared;
        this.runtimeSettings = runtimeSettings;
    }

    public bool HasAttemptsRemaining(int completedAttemptCount) => completedAttemptCount < MaximumAttempts;

    public DateTimeOffset NextAttemptAt(DateTimeOffset now, int completedAttemptCount, TimeSpan? providerDelay = null)
    {
        var exponent = Math.Min(Math.Max(completedAttemptCount - 1, 0), 10);
        var currentMinimumBackoff = runtimeSettings is null
            ? minimumBackoff
            : TimeSpan.FromSeconds(Math.Max(1, runtimeSettings.Current.SendGrid.MinimumBackoffSeconds));
        var currentMaximumBackoff = runtimeSettings is null
            ? maximumBackoff
            : TimeSpan.FromSeconds(Math.Max(currentMinimumBackoff.TotalSeconds, runtimeSettings.Current.SendGrid.MaximumBackoffSeconds));
        var exponentialSeconds = currentMinimumBackoff.TotalSeconds * Math.Pow(2, exponent);
        var jitterSeconds = random.NextDouble() * Math.Min(currentMinimumBackoff.TotalSeconds, 5);
        var bounded = TimeSpan.FromSeconds(Math.Min(currentMaximumBackoff.TotalSeconds, exponentialSeconds + jitterSeconds));
        if (providerDelay is TimeSpan delay && delay > bounded)
        {
            bounded = delay > currentMaximumBackoff ? currentMaximumBackoff : delay;
        }

        return now.Add(bounded);
    }

    public int MaximumAttempts => runtimeSettings?.Current.SendGrid.MaximumAttempts is int configured
        ? Math.Clamp(configured, 1, 10)
        : maximumAttempts;
}