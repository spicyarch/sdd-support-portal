namespace SupportPortal.Contracts.Authorization;

public sealed record BootstrapPortalRequest(
    string DisplayName,
    string Email);

public sealed record BootstrapPortalResponse(
    Guid UserId,
    string Role,
    bool BootstrapDisabled,
    string RowVersion);
