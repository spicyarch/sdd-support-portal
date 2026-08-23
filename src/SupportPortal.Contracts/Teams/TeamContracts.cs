namespace SupportPortal.Contracts.Teams;

public sealed record CreateTeamRequest(string Name);

public sealed record UpdateTeamRequest(string? Name, string? Status);

public sealed record TeamResponse(
    Guid TeamId,
    string Name,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeactivatedAt,
    string RowVersion);

public sealed record TeamCollectionResponse(IReadOnlyList<TeamResponse> Items);