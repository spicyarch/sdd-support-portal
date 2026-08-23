using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Middleware;

namespace SupportPortal.Api.Endpoints;

public sealed class CorsEndpoint
{
    [Function("Cors")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "options", Route = "{*route}")] HttpRequest request)
    {
        ApiResponse.AddCorsHeaders(request);
        return new StatusCodeResult(StatusCodes.Status204NoContent);
    }
}