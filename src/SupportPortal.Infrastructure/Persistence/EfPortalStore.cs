using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using SupportPortal.Application.Abstractions;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.SupportRequests;
using SupportPortal.Domain.Teams;

namespace SupportPortal.Infrastructure.Persistence;

public sealed class EfPortalStore(SupportPortalDbContext dbContext) : IPortalStore
{
    private readonly object syncRoot = new();

    public IReadOnlyList<Team> GetTeams() => dbContext.Teams.ToArray();

    public Team? GetTeam(Guid teamId) => dbContext.Teams.SingleOrDefault(team => team.TeamId == teamId);

    public IReadOnlyList<User> GetUsers() => dbContext.Users.ToArray();

    public User? GetUser(Guid userId) => dbContext.Users.SingleOrDefault(user => user.UserId == userId);

    public User? FindUser(Guid tenantId, Guid objectId) => dbContext.Users.SingleOrDefault(user => user.TenantId == tenantId && user.ObjectId == objectId);

    public IReadOnlyList<RoleAssignment> GetRoleAssignments() => dbContext.RoleAssignments.ToArray();

    public RoleAssignment? GetActiveRoleAssignment(Guid userId) => dbContext.RoleAssignments.SingleOrDefault(item => item.UserId == userId && item.RevokedAt == null);

    public IReadOnlyList<Invitation> GetInvitations() => dbContext.Invitations.ToArray();

    public Invitation? GetInvitation(Guid invitationId) => dbContext.Invitations.SingleOrDefault(invitation => invitation.InvitationId == invitationId);

    public IReadOnlyList<SupportRequest> GetRequests() => dbContext.SupportRequests.ToArray();

    public SupportRequest? GetRequest(Guid requestId) => dbContext.SupportRequests
        .Include(request => request.Messages)
        .SingleOrDefault(request => request.SupportRequestId == requestId);

    public IReadOnlyList<AuditEvent> GetAuditEvents() => dbContext.AuditEvents.ToArray();

    public CommandReceipt? GetCommandReceipt(Guid actorUserId, Guid idempotencyKey) => dbContext.CommandReceipts
        .SingleOrDefault(receipt => receipt.ActorUserId == actorUserId && receipt.IdempotencyKey == idempotencyKey);

    public void AddTeam(Team team) => dbContext.Teams.Add(team);

    public void AddUser(User user) => dbContext.Users.Add(user);

    public void AddRoleAssignment(RoleAssignment assignment) => dbContext.RoleAssignments.Add(assignment);

    public void AddInvitation(Invitation invitation) => dbContext.Invitations.Add(invitation);

    public void AddRequest(SupportRequest request) => dbContext.SupportRequests.Add(request);

    public void AddAuditEvent(AuditEvent auditEvent) => dbContext.AuditEvents.Add(auditEvent);

    public void AddCommandReceipt(CommandReceipt receipt) => dbContext.CommandReceipts.Add(receipt);

    public void Execute(Action action)
    {
        Execute(() =>
        {
            action();
            return true;
        });
    }

    public T Execute<T>(Func<T> action)
        => ExecuteWithTransaction(action, null);

    public T ExecuteSerializable<T>(Func<T> action)
        => ExecuteWithTransaction(action, IsolationLevel.Serializable);

    private T ExecuteWithTransaction<T>(Func<T> action, IsolationLevel? isolationLevel)
    {
        lock (syncRoot)
        {
            IDbContextTransaction? transaction = null;
            if (dbContext.Database.IsRelational())
            {
                transaction = isolationLevel is IsolationLevel level
                    ? dbContext.Database.BeginTransaction(level)
                    : dbContext.Database.BeginTransaction();
            }

            try
            {
                var result = action();
                dbContext.SaveChanges();
                transaction?.Commit();
                return result;
            }
            catch
            {
                transaction?.Rollback();
                dbContext.ChangeTracker.Clear();
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }
    }
}
