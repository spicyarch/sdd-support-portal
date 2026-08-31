using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Auth;
using SupportPortal.Api.Middleware;
using SupportPortal.Application.Common;
using SupportPortal.Application.Settings;
using SupportPortal.Contracts.Settings;

namespace SupportPortal.Api.Endpoints;

public sealed class GlobalSettingsEndpoint(
    EntraClaimsPrincipalFactory identityFactory,
    GlobalSettingsService settings)
{
    [Function("GetGlobalSettings")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/settings")] HttpRequest request)
    {
        try
        {
            var principal = identityFactory.Resolve(request);
            var response = await settings.GetAsync(principal, request.HttpContext.RequestAborted);
            var ifNoneMatch = request.Headers.IfNoneMatch.ToString().Trim('"');
            if (StringComparer.Ordinal.Equals(ifNoneMatch, response.SettingsVersion))
            {
                return ApiResponse.NotModified(request, response.SettingsVersion);
            }

            return ApiResponse.Json(request, response, etag: response.SettingsVersion);
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    [Function("ReplaceGlobalSettings")]
    public async Task<IActionResult> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/settings")] HttpRequest request)
    {
        try
        {
            var input = await request.ReadFromJsonAsync<UpdateGlobalSettingsRequest>() ??
                throw new PortalServiceException(400, "Invalid settings request", "A settings request body is required.");
            var principal = identityFactory.Resolve(request);
            var expectedVersion = RequireHeader(request, "If-Match");
            var idempotencyKey = RequireIdempotencyKey(request);
            var correlationId = Activity.Current?.Id ?? request.HttpContext.TraceIdentifier;
            var response = await settings.ReplaceAsync(
                principal,
                expectedVersion,
                idempotencyKey,
                input,
                correlationId,
                request.HttpContext.RequestAborted);
            return ApiResponse.Json(request, response, etag: response.SettingsVersion);
        }
        catch (JsonException)
        {
            return ApiResponse.HandleException(
                request,
                new PortalServiceException(400, "Invalid settings request", "The settings request body is invalid."));
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
            throw new PortalServiceException(400, "Invalid idempotency key", "A valid Idempotency-Key header is required.");
        }

        return key;
    }

    private static string RequireHeader(HttpRequest request, string name)
    {
        if (!request.Headers.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new PortalServiceException(400, "Missing concurrency header", $"A {name} header is required.");
        }

        return value.ToString();
    }
}
