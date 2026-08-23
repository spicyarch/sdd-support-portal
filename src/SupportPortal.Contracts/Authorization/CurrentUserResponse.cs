namespace SupportPortal.Contracts.Authorization;

public sealed record CurrentUserResponse(
    Guid UserId,
    string DisplayName,
    string Role,
    Guid? TeamId,
    string? TeamName,
    string Status);