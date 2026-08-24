using System.Security.Cryptography;
using System.Text;
using SupportPortal.Application.Abstractions;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Notifications;
using SupportPortal.Domain.SupportRequests;
using SupportPortal.Application.Common;

namespace SupportPortal.Application.Notifications;

public sealed record NotificationRecipientCandidate(
    NotificationRecipientKind Kind,
    Guid? UserId,
    string? Address,
    string? DisplayName,
    string RecipientKey);

public sealed class NotificationRecipientPlanner
{
    private readonly IPortalStore store;
    private readonly IReadOnlyList<string> configuredGlobalRecipients;

    public NotificationRecipientPlanner(IPortalStore store, IReadOnlyList<string>? configuredGlobalRecipients)
    {
        this.store = store;
        this.configuredGlobalRecipients = NormalizeAddresses(configuredGlobalRecipients ?? []);
    }

    public IReadOnlyList<NotificationRecipientCandidate> Plan(Notification notification)
    {
        return notification.EventType switch
        {
            NotificationEventType.RequestCreated => PlanConfiguredRecipients(notification),
            NotificationEventType.TeamReply => PlanTeamReply(notification),
            NotificationEventType.GlobalSupportReply => PlanGlobalReply(notification),
            NotificationEventType.InvitationCreated => PlanInvitation(notification),
            _ => []
        };
    }

    public IReadOnlyList<NotificationRecipientCandidate> PlanEligible(Notification notification, DateTimeOffset now)
    {
        var planned = Plan(notification);
        return planned.Where(candidate => IsEligibleAt(notification, candidate, now)).ToArray();
    }

    public bool IsEligibleAt(Notification notification, NotificationRecipientCandidate candidate, DateTimeOffset now) =>
        IsEligible(notification, candidate, now);

    private IReadOnlyList<NotificationRecipientCandidate> PlanTeamReply(Notification notification)
    {
        if (notification.SupportRequestId is not Guid requestId)
        {
            return [];
        }

        var request = store.GetRequest(requestId);
        if (request is null)
        {
            return [];
        }

        if (notification.AssigneeUserIdAtEvent is Guid assignee && IsEligibleGlobalUser(assignee))
        {
            return [CreateUserCandidate(assignee)];
        }

        return PlanConfiguredRecipients(notification);
    }

    private IReadOnlyList<NotificationRecipientCandidate> PlanGlobalReply(Notification notification)
    {
        if (notification.SupportRequestId is not Guid requestId)
        {
            return [];
        }

        var request = store.GetRequest(requestId);
        if (request is null)
        {
            return [];
        }

        var candidates = new List<NotificationRecipientCandidate>();
        AddUserCandidateIfEligible(candidates, request.CreatedByUserId, request.TeamId, notification.ActorUserId);
        var sourceMessage = request.Messages.SingleOrDefault(message => message.MessageId == notification.SourceEntityId);
        var cutoff = sourceMessage?.CreatedAt ?? notification.EventOccurredAt;
        foreach (var message in request.Messages.Where(message => message.CreatedAt <= cutoff))
        {
            if (message.AuthorRole is PortalRole.TeamAdministrator or PortalRole.TeamUser)
            {
                AddUserCandidateIfEligible(candidates, message.AuthorUserId, request.TeamId, notification.ActorUserId);
            }
        }

        return Deduplicate(candidates);
    }

    private IReadOnlyList<NotificationRecipientCandidate> PlanInvitation(Notification notification)
    {
        if (notification.InvitationId is not Guid invitationId ||
            store.GetInvitation(invitationId) is not { } invitation ||
            invitation.Status != InvitationStatus.Pending)
        {
            return [];
        }

        return [new NotificationRecipientCandidate(
            NotificationRecipientKind.InvitationRecipient,
            null,
            null,
            null,
            HashKey($"invitation:{invitation.InvitationId}"))];
    }

    private IReadOnlyList<NotificationRecipientCandidate> PlanConfiguredRecipients(Notification notification)
    {
        var actorEmail = NormalizeAddress(store.GetUser(notification.ActorUserId)?.Email);
        var candidates = configuredGlobalRecipients
            .Where(address => !StringComparer.OrdinalIgnoreCase.Equals(address, actorEmail))
            .Where(address => IsConfiguredMailboxEligible(address))
            .Select(address => new NotificationRecipientCandidate(
                NotificationRecipientKind.ConfiguredGlobalMailbox,
                null,
                address,
                null,
                HashKey($"mailbox:{address.ToUpperInvariant()}")))
            .ToArray();
        return Deduplicate(candidates);
    }

    private bool IsEligible(Notification notification, NotificationRecipientCandidate candidate, DateTimeOffset now)
    {
        if (candidate.Kind == NotificationRecipientKind.ConfiguredGlobalMailbox)
        {
            return candidate.Address is not null && IsConfiguredMailboxEligible(candidate.Address);
        }

        if (candidate.Kind == NotificationRecipientKind.InvitationRecipient)
        {
            return notification.InvitationId is Guid invitationId &&
                store.GetInvitation(invitationId) is { Status: InvitationStatus.Pending } invitation &&
                now < invitation.ExpiresAt;
        }

        if (candidate.UserId is not Guid userId || userId == notification.ActorUserId)
        {
            return false;
        }

        var user = store.GetUser(userId);
        var assignment = store.GetActiveRoleAssignment(userId);
        if (user is null || user.Status != UserStatus.Active || !EmailAddressRules.TryNormalize(user.Email, out _) || assignment is null)
        {
            return false;
        }

        if (notification.SupportRequestId is not Guid requestId || store.GetRequest(requestId) is not { } request)
        {
            return false;
        }

        return assignment.Role is PortalRole.GlobalAdministrator or PortalRole.GlobalSupportUser ||
            assignment.TeamId == request.TeamId && assignment.Role is PortalRole.TeamAdministrator or PortalRole.TeamUser;
    }

    private bool IsEligibleGlobalUser(Guid userId)
    {
        var user = store.GetUser(userId);
        var assignment = store.GetActiveRoleAssignment(userId);
        return user?.Status == UserStatus.Active &&
            EmailAddressRules.TryNormalize(user.Email, out _) &&
            assignment?.Role is PortalRole.GlobalAdministrator or PortalRole.GlobalSupportUser;
    }

    private bool IsConfiguredMailboxEligible(string address)
    {
        var matches = store.GetUsers()
            .Where(user => StringComparer.OrdinalIgnoreCase.Equals(NormalizeAddress(user.Email), address))
            .ToArray();
        if (matches.Length == 0)
        {
            return true;
        }

        return matches.Length == 1 && IsEligibleGlobalUser(matches[0].UserId);
    }

    private void AddUserCandidateIfEligible(List<NotificationRecipientCandidate> candidates, Guid userId, Guid teamId, Guid actorUserId)
    {
        if (userId == actorUserId)
        {
            return;
        }

        var user = store.GetUser(userId);
        var assignment = store.GetActiveRoleAssignment(userId);
        if (user?.Status != UserStatus.Active || !EmailAddressRules.TryNormalize(user.Email, out _) || assignment is null ||
            !((assignment.Role is PortalRole.GlobalAdministrator or PortalRole.GlobalSupportUser) ||
              assignment.TeamId == teamId && assignment.Role is PortalRole.TeamAdministrator or PortalRole.TeamUser))
        {
            return;
        }

        candidates.Add(CreateUserCandidate(userId));
    }

    private NotificationRecipientCandidate CreateUserCandidate(Guid userId)
    {
        var user = store.GetUser(userId) ?? throw new InvalidOperationException("Notification recipient user was not found.");
        return new NotificationRecipientCandidate(
            NotificationRecipientKind.PortalUser,
            user.UserId,
            null,
            user.DisplayName,
            HashKey($"user:{user.UserId}"));
    }

    private IReadOnlyList<NotificationRecipientCandidate> Deduplicate(IEnumerable<NotificationRecipientCandidate> candidates) =>
        candidates
            .Where(candidate => candidate.UserId is not null || candidate.Address is not null || candidate.Kind == NotificationRecipientKind.InvitationRecipient)
            .GroupBy(candidate => candidate.RecipientKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

    private static IReadOnlyList<string> NormalizeAddresses(IEnumerable<string> addresses) =>
        addresses
            .Select(NormalizeAddress)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string NormalizeAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return EmailAddressRules.TryNormalize(value, out var normalized) ? normalized : string.Empty;
    }

    private static string HashKey(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}