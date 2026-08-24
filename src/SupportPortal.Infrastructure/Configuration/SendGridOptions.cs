namespace SupportPortal.Infrastructure.Configuration;

public sealed class SendGridOptions
{
    public bool Enabled { get; set; }

    public string? ApiKey { get; set; }

    public string? SenderDisplayName { get; set; }

    public string? SenderAddress { get; set; }

    public string? ReplyToAddress { get; set; }

    public IReadOnlyList<string> GlobalSupportRecipients { get; set; } = [];

    public string? PublicPortalUrl { get; set; }

    public int HttpTimeoutSeconds { get; set; } = 15;

    public int MaximumAttempts { get; set; } = 4;

    public int MinimumBackoffSeconds { get; set; } = 5;

    public int MaximumBackoffSeconds { get; set; } = 60;

    public string DataResidency { get; set; } = "Global";

    public int BatchSize { get; set; } = 25;

    public int LeaseSeconds { get; set; } = 60;
}