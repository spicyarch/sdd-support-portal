namespace SupportPortal.Application.Authorization;

public sealed record AuthenticatedIdentity(
    Guid TenantId,
    Guid ObjectId,
    string DisplayName,
    string Email);
