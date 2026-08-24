namespace SupportPortal.Application.Notifications;

public sealed record BrandedEmailContent(string Subject, string PlainText, string Html);