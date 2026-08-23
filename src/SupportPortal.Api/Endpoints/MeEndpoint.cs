using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Auth;
using SupportPortal.Api.Middleware;
using SupportPortal.Application;

namespace SupportPortal.Api.Endpoints;

public sealed class MeEndpoint
{
    private readonly EntraClaimsPrincipalFactory identityFactory;
    private readonly SupportPortalService portal;

    public MeEndpoint(EntraClaimsPrincipalFactory identityFactory, SupportPortalService portal)
    {
        this.identityFactory = identityFactory;
        this.portal = portal;
    }

    [Function("GetCurrentUser")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/me")] HttpRequest request)
    {
        try
        {
            var principal = identityFactory.Resolve(request);
            return ApiResponse.Json(request, portal.GetCurrentUser(principal));
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }
}