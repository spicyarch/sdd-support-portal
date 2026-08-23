using Serilog;
using Serilog.Formatting.Compact;

namespace SupportPortal.Api.Configuration;

public static class SerilogConfiguration
{
    public static LoggerConfiguration AddPortalDefaults(this LoggerConfiguration loggerConfiguration) =>
        loggerConfiguration
            .Enrich.FromLogContext()
            .WriteTo.Console(new RenderedCompactJsonFormatter());
}