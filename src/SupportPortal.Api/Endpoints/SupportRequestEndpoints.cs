using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Auth;
using SupportPortal.Api.Middleware;
using SupportPortal.Application;
using SupportPortal.Contracts.Requests;

namespace SupportPortal.Api.Endpoints;

public sealed class SupportRequestEndpoints
{
    private readonly EntraClaimsPrincipalFactory identityFactory;
    private readonly SupportPortalService portal;

    public SupportRequestEndpoints(EntraClaimsPrincipalFactory identityFactory, SupportPortalService portal)
    {
        this.identityFactory = identityFactory;
        this.portal = portal;
    }

    [Function("ListSupportRequests")]
    public IActionResult List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/requests")] HttpRequest request)
    {
        try
        {
            var principal = identityFactory.Resolve(request);
            var teamId = TryParseGuid(request.Query["teamId"]);
            var assigneeId = TryParseGuid(request.Query["assigneeUserId"]);
            var result = portal.ListRequests(principal, teamId, request.Query["status"].ToString(), request.Query["priority"].ToString(), assigneeId, request.Query["search"].ToString());
            if (request.Headers.IfNoneMatch.ToString().Trim('"') == result.RowVersion)
            {
                return ApiResponse.Json(request, result, etag: result.RowVersion);
            }

            return ApiResponse.Json(request, result, etag: result.RowVersion);
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    [Function("CreateSupportRequest")]
    public async Task<IActionResult> Create([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/requests")] HttpRequest request)
    {
        try
        {
            var principal = identityFactory.Resolve(request);
            var input = await request.ReadFromJsonAsync<CreateSupportRequestRequest>() ?? throw new InvalidOperationException("A request body is required.");
            var response = portal.CreateRequest(principal, RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, StatusCodes.Status201Created, response.RowVersion);
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    [Function("GetSupportRequest")]
    public IActionResult Get([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/requests/{requestId:guid}")] HttpRequest request, Guid requestId)
    {
        try
        {
            var principal = identityFactory.Resolve(request);
            var response = portal.GetRequest(principal, requestId);
            if (request.Headers.IfNoneMatch.ToString().Trim('"') == response.RowVersion)
            {
                return ApiResponse.Json(request, response, etag: response.RowVersion);
            }

            return ApiResponse.Json(request, response, etag: response.RowVersion);
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    [Function("PostRequestMessage")]
    public async Task<IActionResult> PostMessage([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/requests/{requestId:guid}/messages")] HttpRequest request, Guid requestId)
    {
        try
        {
            var principal = identityFactory.Resolve(request);
            var input = await request.ReadFromJsonAsync<CreateMessageRequest>() ?? throw new InvalidOperationException("A request body is required.");
            var response = portal.PostMessage(principal, requestId, RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, StatusCodes.Status201Created);
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    [Function("ChangeSupportRequestState")]
    public async Task<IActionResult> ChangeState([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/requests/{requestId:guid}/state")] HttpRequest request, Guid requestId)
    {
        try
        {
            var principal = identityFactory.Resolve(request);
            var input = await request.ReadFromJsonAsync<ChangeRequestStateRequest>() ?? throw new InvalidOperationException("A request body is required.");
            var response = portal.ChangeState(principal, requestId, RequireIfMatch(request), RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, etag: response.RowVersion);
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    [Function("ChangeSupportRequestPriority")]
    public async Task<IActionResult> ChangePriority([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/requests/{requestId:guid}/priority")] HttpRequest request, Guid requestId)
    {
        try
        {
            var principal = identityFactory.Resolve(request);
            var input = await request.ReadFromJsonAsync<ChangeRequestPriorityRequest>() ?? throw new InvalidOperationException("A request body is required.");
            var response = portal.ChangePriority(principal, requestId, RequireIfMatch(request), RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, etag: response.RowVersion);
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    [Function("AssignSupportRequest")]
    public async Task<IActionResult> Assign([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/requests/{requestId:guid}/assignment")] HttpRequest request, Guid requestId)
    {
        try
        {
            var principal = identityFactory.Resolve(request);
            var input = await request.ReadFromJsonAsync<AssignRequestRequest>() ?? throw new InvalidOperationException("A request body is required.");
            var response = portal.AssignRequest(principal, requestId, RequireIfMatch(request), RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, etag: response.RowVersion);
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

    private static string RequireIfMatch(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("If-Match", out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("An If-Match header is required.");
        }

        return value.ToString();
    }

    private static Guid? TryParseGuid(string? value) => Guid.TryParse(value, out var result) ? result : null;
}