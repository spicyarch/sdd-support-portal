using System.Net;
using System.Net.Http.Json;
using SupportPortal.Contracts.Auditing;
using SupportPortal.Contracts.Authorization;
using SupportPortal.Contracts.Requests;
using SupportPortal.Contracts.Teams;

namespace SupportPortal.Client.Services;

public sealed class SupportPortalApiClient
{
    private readonly HttpClient httpClient;

    public SupportPortalApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("me", cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<CurrentUserResponse>(cancellationToken: cancellationToken);
    }

    public async Task<SupportRequestPageResponse?> ListRequestsAsync(string? etag = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "requests");
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{etag}\"");
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return null;
        }

        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<SupportRequestPageResponse>(cancellationToken: cancellationToken);
    }

    public async Task<SupportRequestPageResponse?> ListRequestsAsync(string? search, string? status, string? priority, Guid? assigneeUserId, string? etag, CancellationToken cancellationToken = default)
        => await ListRequestsAsync(search, status, priority, null, assigneeUserId, etag, cancellationToken);

    public async Task<SupportRequestPageResponse?> ListRequestsAsync(string? search, string? status, string? priority, Guid? teamId, Guid? assigneeUserId, string? etag, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (teamId is Guid team)
        {
            query.Add($"teamId={team}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query.Add($"priority={Uri.EscapeDataString(priority)}");
        }

        if (assigneeUserId is Guid assignee)
        {
            query.Add($"assigneeUserId={assignee}");
        }

        var path = query.Count == 0 ? "requests" : $"requests?{string.Join('&', query)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{etag}\"");
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return null;
        }

        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<SupportRequestPageResponse>(cancellationToken: cancellationToken);
    }

    public async Task<SupportRequestDetailResponse?> GetRequestAsync(Guid requestId, string? etag = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"requests/{requestId}");
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{etag}\"");
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return null;
        }

        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<SupportRequestDetailResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<SupportRequestDetailResponse> CreateRequestAsync(CreateSupportRequestRequest input, CancellationToken cancellationToken = default)
    {
        using var request = CreateMutationRequest(HttpMethod.Post, "requests", input);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<SupportRequestDetailResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<MessageResponse> PostMessageAsync(Guid requestId, CreateMessageRequest input, CancellationToken cancellationToken = default)
    {
        using var request = CreateMutationRequest(HttpMethod.Post, $"requests/{requestId}/messages", input);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<MessageResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<SupportRequestDetailResponse> ChangeStateAsync(Guid requestId, string rowVersion, ChangeRequestStateRequest input, CancellationToken cancellationToken = default) =>
        await SendRequestUpdateAsync(requestId, "state", rowVersion, input, cancellationToken);

    public async Task<SupportRequestDetailResponse> ChangePriorityAsync(Guid requestId, string rowVersion, ChangeRequestPriorityRequest input, CancellationToken cancellationToken = default) =>
        await SendRequestUpdateAsync(requestId, "priority", rowVersion, input, cancellationToken);

    public async Task<SupportRequestDetailResponse> AssignRequestAsync(Guid requestId, string rowVersion, AssignRequestRequest input, CancellationToken cancellationToken = default) =>
        await SendRequestUpdateAsync(requestId, "assignment", rowVersion, input, cancellationToken);

    public async Task<TeamCollectionResponse> GetTeamsAsync(CancellationToken cancellationToken = default) =>
        await GetJsonAsync<TeamCollectionResponse>("teams", cancellationToken);

    public async Task<TeamResponse> CreateTeamAsync(CreateTeamRequest input, CancellationToken cancellationToken = default) =>
        await SendJsonMutationAsync<TeamResponse>(HttpMethod.Post, "teams", input, cancellationToken);

    public async Task<TeamResponse> UpdateTeamAsync(Guid teamId, string rowVersion, UpdateTeamRequest input, CancellationToken cancellationToken = default)
    {
        using var request = CreateMutationRequest(HttpMethod.Patch, $"teams/{teamId}", input);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{rowVersion}\"");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<TeamResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<MembershipCollectionResponse> GetMembershipsAsync(CancellationToken cancellationToken = default) =>
        await GetJsonAsync<MembershipCollectionResponse>("memberships", cancellationToken);

    public async Task<MembershipResponse> CreateMembershipAsync(CreateMembershipRequest input, CancellationToken cancellationToken = default) =>
        await SendJsonMutationAsync<MembershipResponse>(HttpMethod.Post, "memberships", input, cancellationToken);

    public async Task<InvitationCreatedResponse> CreateInvitationAsync(CreateInvitationRequest input, CancellationToken cancellationToken = default) =>
        await SendJsonMutationAsync<InvitationCreatedResponse>(HttpMethod.Post, "invitations", input, cancellationToken);

    public async Task<CurrentUserResponse> AcceptInvitationAsync(AcceptInvitationRequest input, CancellationToken cancellationToken = default) =>
        await SendJsonMutationAsync<CurrentUserResponse>(HttpMethod.Post, "invitations/accept", input, cancellationToken);

    public async Task<MembershipResponse> ChangeMembershipAsync(Guid roleAssignmentId, string rowVersion, ChangeMembershipRequest input, CancellationToken cancellationToken = default)
    {
        using var request = CreateMutationRequest(HttpMethod.Patch, $"memberships/{roleAssignmentId}", input);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{rowVersion}\"");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<MembershipResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<UserStatusResponse> ChangeUserStatusAsync(Guid userId, string rowVersion, ChangeUserStatusRequest input, CancellationToken cancellationToken = default)
    {
        using var request = CreateMutationRequest(HttpMethod.Patch, $"users/{userId}/status", input);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{rowVersion}\"");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<UserStatusResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<AuditEventCollectionResponse> GetAuditEventsAsync(CancellationToken cancellationToken = default) =>
        await GetJsonAsync<AuditEventCollectionResponse>("audit-events", cancellationToken);

    private async Task<SupportRequestDetailResponse> SendRequestUpdateAsync<T>(Guid requestId, string action, string rowVersion, T input, CancellationToken cancellationToken)
    {
        using var request = CreateMutationRequest(HttpMethod.Patch, $"requests/{requestId}/{action}", input);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{rowVersion}\"");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<SupportRequestDetailResponse>(cancellationToken: cancellationToken))!;
    }

    private static HttpRequestMessage CreateMutationRequest<T>(HttpMethod method, string path, T input)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(input) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        return request;
    }

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken))!;
    }

    private async Task<T> SendJsonMutationAsync<T>(HttpMethod method, string path, object input, CancellationToken cancellationToken)
    {
        using var request = CreateMutationRequest(method, path, input);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken))!;
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? $"Request failed with {(int)response.StatusCode}." : detail);
    }
}