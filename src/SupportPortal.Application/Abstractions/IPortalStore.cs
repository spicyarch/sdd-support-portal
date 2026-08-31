using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.SupportRequests;
using SupportPortal.Domain.Teams;
using SupportPortal.Domain.Notifications;
using SupportPortal.Domain.Settings;

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

    IReadOnlyList<CommandReceipt> GetCommandReceipts();

    IReadOnlyList<Notification> GetNotifications();

    Notification? GetNotification(Guid notificationId);

    Notification? GetNotification(NotificationEventType eventType, Guid sourceEntityId);

    IReadOnlyList<NotificationDelivery> GetNotificationDeliveries(Guid notificationId);

    int GetNotificationDeliveriesByState(NotificationDeliveryState state);

    NotificationDelivery? GetNotificationDelivery(Guid notificationDeliveryId);

    IReadOnlyList<NotificationAttempt> GetNotificationAttempts(Guid notificationDeliveryId);

    DeploymentSettings? GetDeploymentSettings();

    IReadOnlyList<DeploymentSettingsRecipient> GetDeploymentSettingsRecipients(Guid deploymentSettingsId);

    IReadOnlyList<NotificationDelivery> GetDueNotificationDeliveries(DateTimeOffset now, int maximumCount);

    (Notification Notification, NotificationDelivery Delivery, NotificationAttempt Attempt)? TryStartNotificationAttempt(
        Guid deliveryId,
        string leaseOwner,
        DateTimeOffset now,
        TimeSpan leaseDuration);

    void AddTeam(Team team);

    void AddUser(User user);

    void AddRoleAssignment(RoleAssignment assignment);

    void AddInvitation(Invitation invitation);

    void AddRequest(SupportRequest request);

    void AddAuditEvent(AuditEvent auditEvent);

    void AddCommandReceipt(CommandReceipt receipt);

    void AddNotification(Notification notification);

    void AddNotificationDelivery(NotificationDelivery delivery);

    void AddNotificationAttempt(NotificationAttempt attempt);

    void AddDeploymentSettings(DeploymentSettings settings);

    void AddDeploymentSettingsRecipient(DeploymentSettingsRecipient recipient);

    void RemoveDeploymentSettingsRecipients(Guid deploymentSettingsId);

    void Execute(Action action);

    T Execute<T>(Func<T> action);

    T ExecuteSerializable<T>(Func<T> action);
}