namespace SupportPortal.Contracts.Requests;

public sealed record CreateSupportRequestRequest(
    string Subject,
    string Priority,
    string Description);

public sealed record CreateMessageRequest(
    string Body,
    Guid ClientMutationId);

public sealed record ChangeRequestStateRequest(string Status);

public sealed record ChangeRequestPriorityRequest(string Priority);

public sealed record AssignRequestRequest(Guid? AssigneeUserId);

public sealed record SupportRequestSummaryResponse(
    Guid RequestId,
    string Reference,
    Guid TeamId,
    string Subject,
    string Priority,
    string Status,
    Guid? AssigneeUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string RowVersion);

public sealed record MessageResponse(
    Guid MessageId,
    Guid AuthorUserId,
    string AuthorRole,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record SupportRequestDetailResponse(
    Guid RequestId,
    string Reference,
    Guid TeamId,
    string Subject,
    string Description,
    string Priority,
    string Status,
    Guid? AssigneeUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string RowVersion,
    IReadOnlyList<MessageResponse> Messages);

public sealed record SupportRequestPageResponse(
    IReadOnlyList<SupportRequestSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string RowVersion);