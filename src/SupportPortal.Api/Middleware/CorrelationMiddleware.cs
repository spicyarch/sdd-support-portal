using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace SupportPortal.Api.Middleware;

public sealed class CorrelationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<CorrelationMiddleware> logger;

    public CorrelationMiddleware(ILogger<CorrelationMiddleware> logger)
    {
        this.logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var correlationId = context.BindingContext.BindingData.TryGetValue("correlationId", out var value)
            ? value?.ToString()
            : null;
        using (logger.BeginScope(new Dictionary<string, object?> { ["correlationId"] = correlationId ?? Activity.Current?.Id }))
        {
            await next(context);
        }
    }
}