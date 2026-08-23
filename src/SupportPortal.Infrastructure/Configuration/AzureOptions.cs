namespace SupportPortal.Infrastructure.Configuration;

public sealed class AzureOptions
{
    public string? SqlConnection { get; set; }

    public string? KeyVaultUri { get; set; }

    public string? ApplicationInsightsConnectionString { get; set; }

    public string AuthenticationMode { get; set; } = "Development";

    public bool DevelopmentIdentitiesEnabled { get; set; }

    public bool BootstrapEnabled { get; set; }

    public Guid? BootstrapTenantId { get; set; }

    public Guid? BootstrapObjectId { get; set; }

    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];

    public string? InvitationTokenKey { get; set; }

    public string? InvitationAcceptanceBaseUrl { get; set; }

    public int InvitationLifetimeHours { get; set; } = 72;
}