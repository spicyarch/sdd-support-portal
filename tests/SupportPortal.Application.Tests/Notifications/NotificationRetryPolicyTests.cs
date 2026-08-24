using SupportPortal.Application.Notifications;

namespace SupportPortal.Application.Tests.Notifications;

public sealed class NotificationRetryPolicyTests
{
    [Fact]
    public void BackoffIsFiniteAndClampedToConfiguredMaximum()
    {
        var policy = new NotificationRetryPolicy(4, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(12), new Random(1));
        var now = DateTimeOffset.UtcNow;

        var next = policy.NextAttemptAt(now, 4);

        Assert.InRange(next - now, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(12));
        Assert.False(policy.HasAttemptsRemaining(4));
        Assert.True(policy.HasAttemptsRemaining(3));
    }

    [Fact]
    public void ProviderDelayIsClampedToConfiguredMaximum()
    {
        var policy = new NotificationRetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), new Random(1));
        var now = DateTimeOffset.UtcNow;

        var next = policy.NextAttemptAt(now, 1, TimeSpan.FromMinutes(2));

        Assert.Equal(TimeSpan.FromSeconds(10), next - now);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public void SendGridTransientStatusesAreRetryable(int statusCode)
    {
        Assert.True(SendGridFailureClassifier.IsRetryable(statusCode));
    }

    [Theory]
    [InlineData(400, "RequestRejected")]
    [InlineData(401, "AuthenticationRejected")]
    [InlineData(403, "PermissionOrSenderRejected")]
    [InlineData(404, "RequestRejected")]
    public void SendGridNonSuccessStatusesUseSafeCategories(int statusCode, string category)
    {
        Assert.Equal(category, SendGridFailureClassifier.Classify(statusCode));
    }
}