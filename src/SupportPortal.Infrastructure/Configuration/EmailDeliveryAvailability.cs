namespace SupportPortal.Infrastructure.Configuration;

public enum EmailDeliveryState
{
    Disabled,
    Ready,
    InvalidConfiguration
}

public sealed record EmailDeliveryAvailability(
    EmailDeliveryState State,
    IReadOnlyList<string> InvalidSettingNames,
    DateTimeOffset CheckedAt)
{
    public bool CanSend => State == EmailDeliveryState.Ready;

    public static EmailDeliveryAvailability Disabled(DateTimeOffset checkedAt) =>
        new(EmailDeliveryState.Disabled, [], checkedAt);
}