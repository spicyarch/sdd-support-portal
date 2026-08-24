using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Json;
using SupportPortal.Application;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Application.Branding;
using SupportPortal.Api.Auth;
using SupportPortal.Api.Configuration;
using SupportPortal.Api.Middleware;
using SupportPortal.Application.Notifications;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Email;
using SupportPortal.Infrastructure.Persistence;
using SupportPortal.Infrastructure.Persistence.Bootstrap;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<CorrelationMiddleware>();
builder.Services.AddAzureConfiguration(builder.Configuration);
builder.Services.AddPortalPersistence(builder.Configuration);
builder.Services.AddSendGridEmail(builder.Configuration.GetSection("SendGrid").Get<SendGridOptions>() ?? new SendGridOptions());
builder.Services.AddScoped<NotificationScheduler>(serviceProvider =>
	new NotificationScheduler(
		serviceProvider.GetRequiredService<IPortalStore>(),
		serviceProvider.GetRequiredService<SendGridOptions>().Enabled));
builder.Services.AddScoped<NotificationRecipientPlanner>(serviceProvider =>
	new NotificationRecipientPlanner(
		serviceProvider.GetRequiredService<IPortalStore>(),
		serviceProvider.GetRequiredService<SendGridOptions>().GlobalSupportRecipients));
builder.Services.AddScoped<NotificationMessageComposer>(serviceProvider =>
	new NotificationMessageComposer(
		serviceProvider.GetRequiredService<IPortalStore>(),
		serviceProvider.GetRequiredService<EffectiveBrandProfile>(),
		serviceProvider.GetRequiredService<SendGridOptions>().PublicPortalUrl ?? "http://localhost:5258",
		serviceProvider.GetRequiredService<IInvitationTokenService>()));
builder.Services.AddSingleton<NotificationRetryPolicy>(serviceProvider =>
{
	var options = serviceProvider.GetRequiredService<SendGridOptions>();
	return new NotificationRetryPolicy(
		options.MaximumAttempts,
		TimeSpan.FromSeconds(options.MinimumBackoffSeconds),
		TimeSpan.FromSeconds(options.MaximumBackoffSeconds));
});
builder.Services.AddScoped<NotificationDeliveryProcessor>(serviceProvider =>
{
	var options = serviceProvider.GetRequiredService<SendGridOptions>();
	var availability = serviceProvider.GetRequiredService<EmailDeliveryAvailability>();
	return new NotificationDeliveryProcessor(
		serviceProvider.GetRequiredService<IPortalStore>(),
		serviceProvider.GetRequiredService<IEmailDeliveryGateway>(),
		serviceProvider.GetRequiredService<NotificationRecipientPlanner>(),
		serviceProvider.GetRequiredService<NotificationMessageComposer>(),
		serviceProvider.GetRequiredService<NotificationRetryPolicy>(),
		serviceProvider.GetRequiredService<TimeProvider>(),
		TimeSpan.FromSeconds(options.LeaseSeconds),
		options.Enabled,
		availability.CanSend,
		options.BatchSize);
});
builder.Services.AddScoped<EmailReadinessService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<SupportPortalService>();
builder.Services.AddScoped<PortalBootstrapService>();
builder.Services.AddScoped<EntraClaimsPrincipalFactory>();
builder.Services.AddOpenTelemetry().UseFunctionsWorkerDefaults();
var applicationInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
	builder.Services.AddOpenTelemetry().UseAzureMonitorExporter();
}

builder.Services.AddSerilog((_, loggerConfiguration) => loggerConfiguration.AddPortalDefaults());

builder.Build().Run();
