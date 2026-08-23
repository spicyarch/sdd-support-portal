using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.SupportRequests;
using SupportPortal.Domain.Teams;

namespace SupportPortal.Application.Abstractions;

public interface IPortalStore
{
    IReadOnlyList<Team> GetTeams();

    Team? GetTeam(Guid teamId);

    IReadOnlyList<User> GetUsers();

    User? GetUser(Guid userId);

    User? FindUser(Guid tenantId, Guid objectId);

    IReadOnlyList<RoleAssignment> GetRoleAssignments();

    RoleAssignment? GetActiveRoleAssignment(Guid userId);

    IReadOnlyList<Invitation> GetInvitations();

    Invitation? GetInvitation(Guid invitationId);

    IReadOnlyList<SupportRequest> GetRequests();

    SupportRequest? GetRequest(Guid requestId);

    IReadOnlyList<AuditEvent> GetAuditEvents();

    CommandReceipt? GetCommandReceipt(Guid actorUserId, Guid idempotencyKey);

    void AddTeam(Team team);

    void AddUser(User user);

    void AddRoleAssignment(RoleAssignment assignment);

    void AddInvitation(Invitation invitation);

    void AddRequest(SupportRequest request);

    void AddAuditEvent(AuditEvent auditEvent);

    void AddCommandReceipt(CommandReceipt receipt);

    void Execute(Action action);

    T Execute<T>(Func<T> action);

    T ExecuteSerializable<T>(Func<T> action);
}