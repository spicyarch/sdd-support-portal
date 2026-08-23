using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Middleware;

namespace SupportPortal.Api.Endpoints;

public sealed class HealthEndpoint
{
    [Function("Health")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest request)
    {
        return ApiResponse.Json(request, new
        {
            status = "ok",
            service = "support-portal-api",
            traceId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier
        });
    }
}