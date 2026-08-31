using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Middleware;
using SupportPortal.Application.Branding;
using SupportPortal.Application.Settings;

namespace SupportPortal.Api.Endpoints;

public sealed class BrandingEndpoint
{
    private readonly EffectiveBrandProfile? legacyBrand;
    private readonly RuntimeSettingsState? runtimeSettings;
    private readonly SettingsRefreshCoordinator? refreshCoordinator;

    public BrandingEndpoint(
        EffectiveBrandProfile brand,
        RuntimeSettingsState runtimeSettings,
        SettingsRefreshCoordinator refreshCoordinator)
    {
        legacyBrand = brand;
        this.runtimeSettings = runtimeSettings;
        this.refreshCoordinator = refreshCoordinator;
    }

    public BrandingEndpoint(EffectiveBrandProfile brand)
    {
        legacyBrand = brand;
    }

    [Function("GetEffectiveBranding")]
    public IActionResult Get([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/branding")] HttpRequest request)
    {
        refreshCoordinator?.RefreshIfDueAsync(request.HttpContext.RequestAborted).GetAwaiter().GetResult();
        var brand = runtimeSettings?.Current.Branding ?? legacyBrand ?? throw new InvalidOperationException("Branding is unavailable.");
        request.HttpContext.Response.Headers.CacheControl = "public, max-age=30";
        var etag = request.Headers.IfNoneMatch.ToString().Trim('"');
        if (StringComparer.Ordinal.Equals(etag, brand.ProfileVersion))
        {
            return ApiResponse.NotModified(request, brand.ProfileVersion);
        }

        return ApiResponse.Json(request, brand.ToResponse(), etag: brand.ProfileVersion);
    }
}