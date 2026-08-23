namespace SupportPortal.Client.Services;

public enum MutationStatus
{
    Idle,
    Pending,
    Succeeded,
    Failed
}

public sealed record MutationState(MutationStatus Status, string? Error = null);