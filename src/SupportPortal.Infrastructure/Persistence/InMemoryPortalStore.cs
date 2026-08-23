using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Authorization;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.SupportRequests;
using SupportPortal.Domain.Teams;

namespace SupportPortal.Infrastructure.Persistence;

public sealed class InMemoryPortalStore : IPortalStore
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, Team> teams = [];
    private readonly Dictionary<Guid, User> users = [];
    private readonly Dictionary<Guid, RoleAssignment> roleAssignments = [];
    private readonly Dictionary<Guid, Invitation> invitations = [];
    private readonly Dictionary<Guid, SupportRequest> requests = [];
    private readonly List<AuditEvent> auditEvents = [];
    private readonly Dictionary<(Guid ActorUserId, Guid IdempotencyKey), CommandReceipt> commandReceipts = [];

    public InMemoryPortalStore(bool seed = true)
    {
        if (seed)
        {
            Seed();
        }
    }

    public IReadOnlyList<Team> GetTeams() => Execute(() => teams.Values.ToArray());

    public Team? GetTeam(Guid teamId) => Execute(() => teams.GetValueOrDefault(teamId));

    public IReadOnlyList<User> GetUsers() => Execute(() => users.Values.ToArray());

    public User? GetUser(Guid userId) => Execute(() => users.GetValueOrDefault(userId));

    public User? FindUser(Guid tenantId, Guid objectId) => Execute(() =>
        users.Values.FirstOrDefault(user => user.TenantId == tenantId && user.ObjectId == objectId));

    public IReadOnlyList<RoleAssignment> GetRoleAssignments() => Execute(() => roleAssignments.Values.ToArray());

    public RoleAssignment? GetActiveRoleAssignment(Guid userId) => Execute(() =>
        roleAssignments.Values.FirstOrDefault(item => item.UserId == userId && item.IsActive));

    public IReadOnlyList<Invitation> GetInvitations() => Execute(() => invitations.Values.ToArray());

    public Invitation? GetInvitation(Guid invitationId) => Execute(() => invitations.GetValueOrDefault(invitationId));

    public IReadOnlyList<SupportRequest> GetRequests() => Execute(() => requests.Values.ToArray());

    public SupportRequest? GetRequest(Guid requestId) => Execute(() => requests.GetValueOrDefault(requestId));

    public IReadOnlyList<AuditEvent> GetAuditEvents() => Execute(() => auditEvents.ToArray());

    public CommandReceipt? GetCommandReceipt(Guid actorUserId, Guid idempotencyKey) => Execute(() =>
        commandReceipts.GetValueOrDefault((actorUserId, idempotencyKey)));

    public void AddTeam(Team team) => Execute(() => teams.Add(team.TeamId, team));

    public void AddUser(User user) => Execute(() => users.Add(user.UserId, user));

    public void AddRoleAssignment(RoleAssignment assignment) => Execute(() => roleAssignments.Add(assignment.RoleAssignmentId, assignment));

    public void AddInvitation(Invitation invitation) => Execute(() => invitations.Add(invitation.InvitationId, invitation));

    public void AddRequest(SupportRequest request) => Execute(() => requests.Add(request.SupportRequestId, request));

    public void AddAuditEvent(AuditEvent auditEvent) => Execute(() => auditEvents.Add(auditEvent));

    public void AddCommandReceipt(CommandReceipt receipt) => Execute(() => commandReceipts.Add((receipt.ActorUserId, receipt.IdempotencyKey), receipt));

    public void Execute(Action action)
    {
        lock (syncRoot)
        {
            action();
        }
    }

    public T Execute<T>(Func<T> action)
    {
        lock (syncRoot)
        {
            return action();
        }
    }

    public T ExecuteSerializable<T>(Func<T> action) => Execute(action);

    private void Seed()
    {
        var now = DateTimeOffset.UtcNow;
        AddTeam(new Team(DevelopmentIdentities.TeamAId, "Team A", now));
        AddTeam(new Team(DevelopmentIdentities.TeamBId, "Team B", now));

        foreach (var identity in DevelopmentIdentities.All)
        {
            AddUser(new User(identity.UserId, DevelopmentIdentities.TenantId, identity.ObjectId, identity.DisplayName, identity.Email, now));
            AddRoleAssignment(new RoleAssignment(Guid.NewGuid(), identity.UserId, identity.Role, identity.TeamId, DevelopmentIdentities.All[0].UserId, now));
        }

        var request = new SupportRequest(
            Guid.Parse("60000000-0000-0000-0000-000000000001"),
            "SP-000001",
            DevelopmentIdentities.TeamAId,
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            "Welcome to the support queue",
            "This seeded request verifies the Team A workspace and global queue.",
            RequestPriority.Normal,
            now);
        request.AddMessage(new Message(Guid.NewGuid(), request.SupportRequestId, request.CreatedByUserId, PortalRole.TeamUser, "Please confirm that the support portal is ready.", Guid.NewGuid(), now), now);
        AddRequest(request);
        AddAuditEvent(new AuditEvent(Guid.NewGuid(), now, "RequestCreated", request.CreatedByUserId, "SupportRequest", request.SupportRequestId, true));
    }
}