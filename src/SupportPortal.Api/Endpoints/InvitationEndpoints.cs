using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Auth;
using SupportPortal.Api.Middleware;
using SupportPortal.Application;
using SupportPortal.Contracts.Authorization;

namespace SupportPortal.Api.Endpoints;

public sealed class InvitationEndpoints
{
    private readonly EntraClaimsPrincipalFactory identityFactory;
    private readonly SupportPortalService portal;

    public InvitationEndpoints(EntraClaimsPrincipalFactory identityFactory, SupportPortalService portal)
    {
        this.identityFactory = identityFactory;
        this.portal = portal;
    }

    [Function("CreateInvitation")]
    public async Task<IActionResult> Create([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/invitations")] HttpRequest request)
    {
        try
        {
            var input = await request.ReadFromJsonAsync<CreateInvitationRequest>() ?? throw new InvalidOperationException("A request body is required.");
            var response = portal.CreateInvitation(identityFactory.Resolve(request), RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, StatusCodes.Status201Created);
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    [Function("AcceptInvitation")]
    public async Task<IActionResult> Accept([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/invitations/accept")] HttpRequest request)
    {
        try
        {
            var input = await request.ReadFromJsonAsync<AcceptInvitationRequest>() ?? throw new InvalidOperationException("A request body is required.");
            var response = portal.AcceptInvitation(identityFactory.ResolveAuthenticatedIdentity(request), RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response);
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
