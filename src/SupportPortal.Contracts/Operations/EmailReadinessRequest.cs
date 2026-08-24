namespace SupportPortal.Contracts.Operations;

public sealed record EmailReadinessRequest(
    string Mode,
    string? TestRecipient = null,
    bool ConfirmLiveSend = false);