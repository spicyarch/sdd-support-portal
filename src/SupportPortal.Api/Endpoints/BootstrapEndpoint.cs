using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Middleware;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Infrastructure.Persistence.Bootstrap;

namespace SupportPortal.Api.Endpoints;

public sealed class BootstrapEndpoint(PortalBootstrapService bootstrap)
{
    [Function("BootstrapPortal")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "bootstrap")] HttpRequest request)
    {
        try
        {
            var input = await request.ReadFromJsonAsync<BootstrapPortalRequest>() ?? throw new InvalidOperationException("A request body is required.");
            var response = bootstrap.Bootstrap(RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, StatusCodes.Status201Created);
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    private static Guid RequireIdempotencyKey(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Idempotency-Key", out var value) || !Guid.TryParse(value, out var key))
        {
            throw new InvalidOperationException("A valid Idempotency-Key header is required.");
        }

        return key;
    }
}
