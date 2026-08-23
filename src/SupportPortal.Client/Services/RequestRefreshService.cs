using SupportPortal.Contracts.Requests;

namespace SupportPortal.Client.Services;

public sealed class RequestRefreshService
{
    private readonly SupportPortalApiClient api;

    public RequestRefreshService(SupportPortalApiClient api)
    {
        this.api = api;
    }

    public Task<SupportRequestPageResponse?> RefreshAsync(string? rowVersion, CancellationToken cancellationToken) => api.ListRequestsAsync(rowVersion, cancellationToken);
}