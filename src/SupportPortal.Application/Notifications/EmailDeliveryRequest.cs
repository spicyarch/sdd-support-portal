namespace SupportPortal.Application.Notifications;

public sealed record EmailDeliveryRequest(
    Guid NotificationId,
    string RecipientAddress,
    string? RecipientDisplayName,
    string SenderAddress,
    string SenderDisplayName,
    string? ReplyToAddress,
    string Subject,
    string PlainTextContent,
    string HtmlContent,
    bool SandboxMode = false);