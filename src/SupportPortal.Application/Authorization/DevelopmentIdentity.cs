using SupportPortal.Domain.Authorization;

namespace SupportPortal.Application.Authorization;

public sealed record DevelopmentIdentity(
    string Key,
    Guid UserId,
    Guid ObjectId,
    string DisplayName,
    string Email,
    PortalRole Role,
    Guid? TeamId);

public static class DevelopmentIdentities
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid TeamAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static readonly Guid TeamBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static readonly IReadOnlyList<DevelopmentIdentity> All =
    [
        new("global-admin", Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("20000000-0000-0000-0000-000000000001"), "Global Administrator", "global-admin@example.test", PortalRole.GlobalAdministrator, null),
        new("global-support", Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("20000000-0000-0000-0000-000000000002"), "Global Support User", "global-support@example.test", PortalRole.GlobalSupportUser, null),
        new("team-admin-a", Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("20000000-0000-0000-0000-000000000003"), "Team A Administrator", "team-admin-a@example.test", PortalRole.TeamAdministrator, TeamAId),
        new("team-user-a", Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("20000000-0000-0000-0000-000000000004"), "Team A User", "team-user-a@example.test", PortalRole.TeamUser, TeamAId),
        new("team-user-b", Guid.Parse("10000000-0000-0000-0000-000000000005"), Guid.Parse("20000000-0000-0000-0000-000000000005"), "Team B User", "team-user-b@example.test", PortalRole.TeamUser, TeamBId)
    ];

    public static bool TryGet(string key, out DevelopmentIdentity? identity)
    {
        identity = All.FirstOrDefault(item => StringComparer.OrdinalIgnoreCase.Equals(item.Key, key));
        return identity is not null;
    }
}