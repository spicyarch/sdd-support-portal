namespace SupportPortal.Contracts.Authorization;

public sealed record CreateMembershipRequest(
    Guid ObjectId,
    string DisplayName,
    string Email,
    string Role,
    Guid? TeamId);

public sealed record ChangeMembershipRequest(
    string Action,
    string? Role,
    Guid? TeamId,
    string? Reason);

public sealed record ChangeUserStatusRequest(string Status, string? Reason);

public sealed record MembershipResponse(
    Guid RoleAssignmentId,
    Guid UserId,
    string DisplayName,
    string Role,
    Guid? TeamId,
    bool Active,
    DateTimeOffset AssignedAt,
    DateTimeOffset? RevokedAt,
    string RowVersion,
    string UserRowVersion);

public sealed record MembershipCollectionResponse(IReadOnlyList<MembershipResponse> Items);

public sealed record UserStatusResponse(Guid UserId, string Status, string RowVersion);