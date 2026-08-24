using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using SupportPortal.Application.Abstractions;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.SupportRequests;
using SupportPortal.Domain.Teams;
using SupportPortal.Domain.Notifications;

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

    public IReadOnlyList<CommandReceipt> GetCommandReceipts() => dbContext.CommandReceipts.ToArray();

    public IReadOnlyList<Notification> GetNotifications() => dbContext.Notifications.ToArray();

    public Notification? GetNotification(Guid notificationId) => dbContext.Notifications
        .SingleOrDefault(notification => notification.NotificationId == notificationId);

    public Notification? GetNotification(NotificationEventType eventType, Guid sourceEntityId) => dbContext.Notifications
        .SingleOrDefault(notification => notification.EventType == eventType && notification.SourceEntityId == sourceEntityId);

    public IReadOnlyList<NotificationDelivery> GetNotificationDeliveries(Guid notificationId) => dbContext.NotificationDeliveries
        .Where(delivery => delivery.NotificationId == notificationId)
        .OrderBy(delivery => delivery.CreatedAt)
        .ToArray();

    public int GetNotificationDeliveriesByState(NotificationDeliveryState state) => dbContext.NotificationDeliveries
        .Count(delivery => delivery.State == state);

    public NotificationDelivery? GetNotificationDelivery(Guid notificationDeliveryId) => dbContext.NotificationDeliveries
        .SingleOrDefault(delivery => delivery.NotificationDeliveryId == notificationDeliveryId);

    public IReadOnlyList<NotificationAttempt> GetNotificationAttempts(Guid notificationDeliveryId) => dbContext.NotificationAttempts
        .Where(attempt => attempt.NotificationDeliveryId == notificationDeliveryId)
        .OrderBy(attempt => attempt.AttemptNumber)
        .ToArray();

    public IReadOnlyList<NotificationDelivery> GetDueNotificationDeliveries(DateTimeOffset now, int maximumCount) => dbContext.NotificationDeliveries
        .Where(delivery =>
            (delivery.State == NotificationDeliveryState.Pending || delivery.State == NotificationDeliveryState.RetryableFailure) &&
            (delivery.NextAttemptAt == null || delivery.NextAttemptAt <= now) &&
            (delivery.LeaseExpiresAt == null || delivery.LeaseExpiresAt <= now))
        .OrderBy(delivery => delivery.NextAttemptAt)
        .Take(maximumCount)
        .ToArray();

    public (Notification Notification, NotificationDelivery Delivery, NotificationAttempt Attempt)? TryStartNotificationAttempt(
        Guid deliveryId,
        string leaseOwner,
        DateTimeOffset now,
        TimeSpan leaseDuration)
        => Execute<(Notification Notification, NotificationDelivery Delivery, NotificationAttempt Attempt)?>(() =>
        {
            var delivery = dbContext.NotificationDeliveries
                .FromSqlInterpolated($"SELECT TOP (1) * FROM [NotificationDeliveries] WITH (UPDLOCK, READPAST, ROWLOCK) WHERE [NotificationDeliveryId] = {deliveryId} AND ([State] = N'Pending' OR [State] = N'RetryableFailure') AND ([NextAttemptAt] IS NULL OR [NextAttemptAt] <= {now}) AND ([LeaseExpiresAt] IS NULL OR [LeaseExpiresAt] <= {now})")
                .SingleOrDefault();
            if (delivery is null)
            {
                return null;
            }

            var notification = dbContext.Notifications.SingleOrDefault(item => item.NotificationId == delivery.NotificationId);
            if (notification is null)
            {
                return null;
            }

            var attempt = delivery.StartAttempt(Guid.NewGuid(), leaseOwner, notification.CorrelationId, now, leaseDuration);
            dbContext.NotificationAttempts.Add(attempt);
            return (notification, delivery, attempt);
        });

    public void AddTeam(Team team) => dbContext.Teams.Add(team);

    public void AddUser(User user) => dbContext.Users.Add(user);

    public void AddRoleAssignment(RoleAssignment assignment) => dbContext.RoleAssignments.Add(assignment);

    public void AddInvitation(Invitation invitation) => dbContext.Invitations.Add(invitation);

    public void AddRequest(SupportRequest request) => dbContext.SupportRequests.Add(request);

    public void AddAuditEvent(AuditEvent auditEvent) => dbContext.AuditEvents.Add(auditEvent);

    public void AddCommandReceipt(CommandReceipt receipt) => dbContext.CommandReceipts.Add(receipt);

    public void AddNotification(Notification notification) => dbContext.Notifications.Add(notification);

    public void AddNotificationDelivery(NotificationDelivery delivery) => dbContext.NotificationDeliveries.Add(delivery);

    public void AddNotificationAttempt(NotificationAttempt attempt) => dbContext.NotificationAttempts.Add(attempt);

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
