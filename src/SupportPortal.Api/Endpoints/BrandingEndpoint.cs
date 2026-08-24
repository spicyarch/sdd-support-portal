using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Middleware;
using SupportPortal.Application.Branding;

namespace SupportPortal.Api.Endpoints;

public sealed class BrandingEndpoint(EffectiveBrandProfile brand)
{
    [Function("GetEffectiveBranding")]
    public IActionResult Get([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/branding")] HttpRequest request)
    {
        request.HttpContext.Response.Headers.CacheControl = "public, max-age=300";
        var etag = request.Headers.IfNoneMatch.ToString().Trim('"');
        if (StringComparer.Ordinal.Equals(etag, brand.ProfileVersion))
        {
            return ApiResponse.NotModified(request, brand.ProfileVersion);
        }

        return ApiResponse.Json(request, brand.ToResponse(), etag: brand.ProfileVersion);
    }
}