using Microsoft.Extensions.DependencyInjection;
using SendGrid.Extensions.DependencyInjection;
using SendGrid.Helpers.Reliability;
using SupportPortal.Application.Abstractions;
using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Infrastructure.Email;

public static class SendGridEmailRegistration
{
    public static IServiceCollection AddSendGridEmail(this IServiceCollection services, SendGridOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            services.AddSendGrid(clientOptions =>
            {
                clientOptions.ApiKey = options.ApiKey;
                clientOptions.SetDataResidency(
                    StringComparer.OrdinalIgnoreCase.Equals(options.DataResidency, "Eu") ? "eu" : "global");
                clientOptions.ReliabilitySettings = new ReliabilitySettings();
            }).ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds));
        }

        services.AddScoped<SendGridEmailGateway>();
        services.AddScoped<IEmailDeliveryGateway>(serviceProvider => serviceProvider.GetRequiredService<SendGridEmailGateway>());
        services.AddScoped<IEmailReadinessGateway>(serviceProvider => serviceProvider.GetRequiredService<SendGridEmailGateway>());
        return services;
    }
}