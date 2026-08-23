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
using SupportPortal.Api.Auth;
using SupportPortal.Api.Configuration;
using SupportPortal.Api.Middleware;
using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence;
using SupportPortal.Infrastructure.Persistence.Bootstrap;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<CorrelationMiddleware>();
builder.Services.AddAzureConfiguration(builder.Configuration);
builder.Services.AddPortalPersistence(builder.Configuration);
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
