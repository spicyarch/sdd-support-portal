using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Authorization;
using SupportPortal.Infrastructure.Persistence.Bootstrap;

namespace SupportPortal.Infrastructure.Configuration;

public static class ManagedIdentityRegistration
{
    public static IServiceCollection AddAzureConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var brandingOptions = configuration.GetSection("Branding").Get<BrandingOptions>() ?? new BrandingOptions();
        var sendGridOptions = configuration.GetSection("SendGrid").Get<SendGridOptions>() ?? new SendGridOptions();
        var checkedAt = DateTimeOffset.UtcNow;
        var brandingValidator = new BrandingOptionsValidator();
        var invalidBrandingSettings = brandingValidator.Validate(brandingOptions, environmentName);
        services.AddSingleton(new AzureOptions
        {
            SqlConnection = configuration["Portal:SqlConnection"],
            KeyVaultUri = configuration["Portal:KeyVaultUri"],
            ApplicationInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"],
            AuthenticationMode = configuration["Portal:AuthenticationMode"] ?? "Development",
            DevelopmentIdentitiesEnabled = bool.TryParse(configuration["Portal:DevelopmentIdentitiesEnabled"], out var enabled) && enabled,
            BootstrapEnabled = bool.TryParse(configuration["Portal:BootstrapEnabled"], out var bootstrapEnabled) && bootstrapEnabled,
            BootstrapTenantId = ParseGuid(configuration["Portal:BootstrapTenantId"]),
            BootstrapObjectId = ParseGuid(configuration["Portal:BootstrapObjectId"]),
            AllowedOrigins = ParseList(configuration["Portal:AllowedOrigins"]),
            InvitationTokenKey = configuration["Portal:InvitationTokenKey"],
            InvitationAcceptanceBaseUrl = configuration["Portal:InvitationAcceptanceBaseUrl"] ?? "http://localhost:5258/invitations/accept",
            InvitationLifetimeHours = int.TryParse(configuration["Portal:InvitationLifetimeHours"], out var lifetimeHours) ? lifetimeHours : 72
        });
        services.AddSingleton(brandingOptions);
        services.AddSingleton(sendGridOptions);
        services.AddSingleton(brandingValidator);
        services.AddSingleton(new BrandingConfigurationStatus(invalidBrandingSettings, checkedAt));
        services.AddSingleton(new SendGridOptionsValidator());
        services.AddSingleton(BrandingResolver.Resolve(
            new BrandingInput(
                brandingOptions.ProductName,
                brandingOptions.ShortProductName,
                brandingOptions.LogoUrl,
                brandingOptions.FaviconUrl,
                brandingOptions.PrimaryColor,
                brandingOptions.AccentColor,
                brandingOptions.FocusColor,
                brandingOptions.SupportContactName,
                brandingOptions.SupportContactEmail,
                brandingOptions.OrganizationName),
            environmentName));
        services.AddSingleton(sp =>
        {
            var validator = sp.GetRequiredService<SendGridOptionsValidator>();
            return validator.Validate(sendGridOptions, environmentName, checkedAt);
        });
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddSingleton<IInvitationTokenService, ConfiguredInvitationTokenService>();
        return services;
    }

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var parsed) ? parsed : null;

    private static IReadOnlyList<string> ParseList(string? value) => (value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}