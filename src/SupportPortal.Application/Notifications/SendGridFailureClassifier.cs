namespace SupportPortal.Application.Notifications;

public static class SendGridFailureClassifier
{
    public static bool IsRetryable(int statusCode) =>
        statusCode == 408 || statusCode == 429 || statusCode is >= 500 and <= 599;

    public static string Classify(int statusCode) => statusCode switch
    {
        401 => "AuthenticationRejected",
        403 => "PermissionOrSenderRejected",
        429 => "RateLimited",
        >= 500 and <= 599 => "ProviderFailure",
        408 => "Timeout",
        400 => "RequestRejected",
        _ => "RequestRejected"
    };
}