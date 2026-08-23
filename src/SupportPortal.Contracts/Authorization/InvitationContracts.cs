namespace SupportPortal.Contracts.Authorization;

public sealed record CreateInvitationRequest(
    string Email,
    string Role,
    Guid? TeamId);

public sealed record InvitationCreatedResponse(
    Guid InvitationId,
    string Role,
    Guid? TeamId,
    string State,
    DateTimeOffset ExpiresAt,
    string AcceptanceLink);

public sealed record AcceptInvitationRequest(string Token);
