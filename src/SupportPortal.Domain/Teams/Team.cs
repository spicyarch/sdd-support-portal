using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.Teams;

public sealed class Team
{
    public Team(Guid teamId, string name, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Team name is required.");
        }

        TeamId = teamId;
        Name = name.Trim();
        CreatedAt = createdAt;
    }

    public Guid TeamId { get; private set; }

    public string Name { get; private set; }

    public TeamStatus Status { get; private set; } = TeamStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public string RowVersion { get; private set; } = "1";

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Team name is required.");
        }

        Name = name.Trim();
        Touch();
    }

    public void Activate()
    {
        Status = TeamStatus.Active;
        DeactivatedAt = null;
        Touch();
    }

    public void Deactivate(DateTimeOffset at)
    {
        Status = TeamStatus.Deactivated;
        DeactivatedAt = at;
        Touch();
    }

    private void Touch()
    {
        RowVersion = Guid.NewGuid().ToString("N");
    }
}