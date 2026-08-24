namespace SupportPortal.Application.Notifications;

public sealed class NotificationRetryPolicy
{
    private readonly int maximumAttempts;
    private readonly TimeSpan minimumBackoff;
    private readonly TimeSpan maximumBackoff;
    private readonly Random random;

    public NotificationRetryPolicy(
        int maximumAttempts,
        TimeSpan minimumBackoff,
        TimeSpan maximumBackoff,
        Random? random = null)
    {
        this.maximumAttempts = Math.Clamp(maximumAttempts, 1, 10);
        this.minimumBackoff = minimumBackoff > TimeSpan.Zero ? minimumBackoff : TimeSpan.FromSeconds(1);
        this.maximumBackoff = maximumBackoff >= this.minimumBackoff ? maximumBackoff : this.minimumBackoff;
        this.random = random ?? Random.Shared;
    }

    public bool HasAttemptsRemaining(int completedAttemptCount) => completedAttemptCount < maximumAttempts;

    public DateTimeOffset NextAttemptAt(DateTimeOffset now, int completedAttemptCount, TimeSpan? providerDelay = null)
    {
        var exponent = Math.Min(Math.Max(completedAttemptCount - 1, 0), 10);
        var exponentialSeconds = minimumBackoff.TotalSeconds * Math.Pow(2, exponent);
        var jitterSeconds = random.NextDouble() * Math.Min(minimumBackoff.TotalSeconds, 5);
        var bounded = TimeSpan.FromSeconds(Math.Min(maximumBackoff.TotalSeconds, exponentialSeconds + jitterSeconds));
        if (providerDelay is TimeSpan delay && delay > bounded)
        {
            bounded = delay > maximumBackoff ? maximumBackoff : delay;
        }

        return now.Add(bounded);
    }

    public int MaximumAttempts => maximumAttempts;
}