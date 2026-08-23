using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using SupportPortal.Api.Auth;
using SupportPortal.Api.Middleware;
using SupportPortal.Application;
using SupportPortal.Contracts.Auditing;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Contracts.Teams;

namespace SupportPortal.Api.Endpoints;

public sealed class AdministrationEndpoints
{
    private readonly EntraClaimsPrincipalFactory identityFactory;
    private readonly SupportPortalService portal;

    public AdministrationEndpoints(EntraClaimsPrincipalFactory identityFactory, SupportPortalService portal)
    {
        this.identityFactory = identityFactory;
        this.portal = portal;
    }

    [Function("ListTeams")]
    public IActionResult ListTeams([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/teams")] HttpRequest request)
    {
        return Execute(request, () => portal.ListTeams(identityFactory.Resolve(request)));
    }

    [Function("CreateTeam")]
    public async Task<IActionResult> CreateTeam([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/teams")] HttpRequest request)
    {
        return await ExecuteAsync(request, async () =>
        {
            var input = await ReadBody<CreateTeamRequest>(request);
            var response = portal.CreateTeam(identityFactory.Resolve(request), RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, StatusCodes.Status201Created, response.RowVersion);
        });
    }

    [Function("UpdateTeam")]
    public async Task<IActionResult> UpdateTeam([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/teams/{teamId:guid}")] HttpRequest request, Guid teamId)
    {
        return await ExecuteAsync(request, async () =>
        {
            var input = await ReadBody<UpdateTeamRequest>(request);
            var response = portal.UpdateTeam(identityFactory.Resolve(request), teamId, RequireIfMatch(request), RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, etag: response.RowVersion);
        });
    }

    [Function("ListMemberships")]
    public IActionResult ListMemberships([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/memberships")] HttpRequest request)
    {
        return Execute(request, () => portal.ListMemberships(identityFactory.Resolve(request), ParseGuid(request.Query["teamId"])));
    }

    [Function("CreateMembership")]
    public async Task<IActionResult> CreateMembership([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/memberships")] HttpRequest request)
    {
        return await ExecuteAsync(request, async () =>
        {
            var input = await ReadBody<CreateMembershipRequest>(request);
            var response = portal.CreateMembership(identityFactory.Resolve(request), RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, StatusCodes.Status201Created, response.RowVersion);
        });
    }

    [Function("ChangeMembership")]
    public async Task<IActionResult> ChangeMembership([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/memberships/{roleAssignmentId:guid}")] HttpRequest request, Guid roleAssignmentId)
    {
        return await ExecuteAsync(request, async () =>
        {
            var input = await ReadBody<ChangeMembershipRequest>(request);
            var response = portal.ChangeMembership(identityFactory.Resolve(request), roleAssignmentId, RequireIfMatch(request), RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, etag: response.RowVersion);
        });
    }

    [Function("ChangeUserStatus")]
    public async Task<IActionResult> ChangeUserStatus([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/users/{userId:guid}/status")] HttpRequest request, Guid userId)
    {
        return await ExecuteAsync(request, async () =>
        {
            var input = await ReadBody<ChangeUserStatusRequest>(request);
            var response = portal.ChangeUserStatus(identityFactory.Resolve(request), userId, RequireIfMatch(request), RequireIdempotencyKey(request), input);
            return ApiResponse.Json(request, response, etag: response.RowVersion);
        });
    }

    [Function("ListAuditEvents")]
    public IActionResult ListAuditEvents([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/audit-events")] HttpRequest request)
    {
        return Execute(request, () => portal.ListAuditEvents(identityFactory.Resolve(request)));
    }

    private IActionResult Execute<T>(HttpRequest request, Func<T> operation)
    {
        try
        {
            return ApiResponse.Json(request, operation()!);
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    private async Task<IActionResult> ExecuteAsync(HttpRequest request, Func<Task<IActionResult>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception)
        {
            return ApiResponse.HandleException(request, exception);
        }
    }

    private static async Task<T> ReadBody<T>(HttpRequest request)
    {
        return await request.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("A request body is required.");
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

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var result) ? result : null;
}