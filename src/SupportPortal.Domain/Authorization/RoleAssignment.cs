namespace SupportPortal.Domain.Authorization;

public sealed class RoleAssignment
{
    public RoleAssignment(
        Guid roleAssignmentId,
        Guid userId,
        PortalRole role,
        Guid? teamId,
        Guid? assignedByUserId,
        DateTimeOffset assignedAt)
    {
        RoleAssignmentId = roleAssignmentId;
        UserId = userId;
        Role = role;
        TeamId = teamId;
        AssignedByUserId = assignedByUserId;
        AssignedAt = assignedAt;
        RoleAssignmentPolicy.ValidateScope(role, teamId);
    }

    public Guid RoleAssignmentId { get; private set; }

    public Guid UserId { get; private set; }

    public PortalRole Role { get; private set; }

    public Guid? TeamId { get; private set; }

    public Guid? AssignedByUserId { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public string? RevocationReason { get; private set; }

    public string RowVersion { get; private set; } = "1";

    public bool IsActive => RevokedAt is null;

    public void Replace(PortalRole role, Guid? teamId)
    {
        RoleAssignmentPolicy.ValidateScope(role, teamId);
        Role = role;
        TeamId = teamId;
        Touch();
    }

    public void Revoke(Guid revokedByUserId, string reason, DateTimeOffset at)
    {
        if (!IsActive)
        {
            return;
        }

        RevokedAt = at;
        RevokedByUserId = revokedByUserId;
        RevocationReason = reason;
        Touch();
    }

    private void Touch()
    {
        RowVersion = Guid.NewGuid().ToString("N");
    }
}