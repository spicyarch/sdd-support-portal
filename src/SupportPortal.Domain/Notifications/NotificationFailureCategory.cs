namespace SupportPortal.Domain.Notifications;

public enum NotificationFailureCategory
{
    None,
    Timeout,
    AmbiguousNetwork,
    RateLimited,
    ProviderFailure,
    RequestRejected,
    AuthenticationRejected,
    PermissionOrSenderRejected,
    InvalidConfiguration,
    Suppressed,
    Unknown
}