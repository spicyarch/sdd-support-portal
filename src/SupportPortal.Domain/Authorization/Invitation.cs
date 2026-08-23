using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.Authorization;

public sealed class Invitation
{
    public Invitation(
        Guid invitationId,
        string email,
        PortalRole role,
        Guid? teamId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        Guid createdByUserId)
    {
        RoleAssignmentPolicy.ValidateScope(role, teamId);
        InvitationId = invitationId;
        Email = email;
        Role = role;
        TeamId = teamId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        CreatedByUserId = createdByUserId;
    }

    public Guid InvitationId { get; private set; }

    public string Email { get; private set; }

    public PortalRole Role { get; private set; }

    public Guid? TeamId { get; private set; }

    public string TokenHash { get; private set; }

    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public void Accept(DateTimeOffset at)
    {
        if (Status != InvitationStatus.Pending || at >= ExpiresAt)
        {
            throw new DomainException("The invitation is no longer valid.");
        }

        Status = InvitationStatus.Accepted;
        AcceptedAt = at;
    }

    public void Revoke(Guid revokedByUserId)
    {
        if (Status == InvitationStatus.Pending)
        {
            Status = InvitationStatus.Revoked;
            RevokedByUserId = revokedByUserId;
        }
    }

    public void Expire(DateTimeOffset at)
    {
        if (Status == InvitationStatus.Pending && at >= ExpiresAt)
        {
            Status = InvitationStatus.Expired;
        }
    }
}