namespace SupportPortal.Domain.Authorization;

public sealed class User
{
    public User(
        Guid userId,
        Guid tenantId,
        Guid objectId,
        string displayName,
        string email,
        DateTimeOffset createdAt)
    {
        UserId = userId;
        TenantId = tenantId;
        ObjectId = objectId;
        DisplayName = displayName;
        Email = email;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ObjectId { get; private set; }

    public string DisplayName { get; private set; }

    public string Email { get; private set; }

    public UserStatus Status { get; private set; } = UserStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public string RowVersion { get; private set; } = "1";

    public void UpdateProfile(string displayName, string email)
    {
        DisplayName = displayName;
        Email = email;
        Touch();
    }

    public void Activate()
    {
        Status = UserStatus.Active;
        DeactivatedAt = null;
        Touch();
    }

    public void Deactivate(DateTimeOffset at)
    {
        Status = UserStatus.Deactivated;
        DeactivatedAt = at;
        Touch();
    }

    private void Touch()
    {
        RowVersion = Guid.NewGuid().ToString("N");
    }
}