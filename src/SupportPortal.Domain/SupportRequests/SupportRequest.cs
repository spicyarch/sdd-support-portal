using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.SupportRequests;

public sealed class SupportRequest
{
    private readonly List<Message> messages = [];

    public SupportRequest(
        Guid supportRequestId,
        string reference,
        Guid teamId,
        Guid createdByUserId,
        string subject,
        string description,
        RequestPriority priority,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Request subject and description are required.");
        }

        SupportRequestId = supportRequestId;
        Reference = reference;
        TeamId = teamId;
        CreatedByUserId = createdByUserId;
        Subject = subject.Trim();
        Description = description.Trim();
        Priority = priority;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid SupportRequestId { get; private set; }

    public string Reference { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string Subject { get; private set; }

    public string Description { get; private set; }

    public RequestPriority Priority { get; private set; }

    public RequestStatus Status { get; private set; } = RequestStatus.New;

    public Guid? AssignedToUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyList<Message> Messages => messages.AsReadOnly();

    public bool ContainsClientMutation(Guid clientMutationId) =>
        messages.Any(message => message.ClientMutationId == clientMutationId);

    public string RowVersion { get; private set; } = "1";

    public void AddMessage(Message message, DateTimeOffset at)
    {
        if (Status == RequestStatus.Closed)
        {
            throw new DomainException("Closed requests are read-only until reopened.");
        }

        if (messages.Any(existing => existing.ClientMutationId == message.ClientMutationId))
        {
            return;
        }

        messages.Add(message);
        if (Status == RequestStatus.Resolved && message.AuthorRole is PortalRole.TeamAdministrator or PortalRole.TeamUser)
        {
            Status = RequestStatus.New;
            ResolvedAt = null;
        }

        Touch(at);
    }

    public void ChangeStatus(RequestStatus status, PortalRole actorRole, DateTimeOffset at)
    {
        SupportRequestStateMachine.ValidateTransition(Status, status, actorRole);
        Status = status;
        if (status == RequestStatus.Resolved)
        {
            ResolvedAt = at;
        }

        if (status == RequestStatus.Closed)
        {
            ClosedAt = at;
        }

        if (status == RequestStatus.New)
        {
            ClosedAt = null;
        }

        Touch(at);
    }

    public void ChangePriority(RequestPriority priority, DateTimeOffset at)
    {
        Priority = priority;
        Touch(at);
    }

    public void Assign(Guid? userId, DateTimeOffset at)
    {
        AssignedToUserId = userId;
        Touch(at);
    }

    private void Touch(DateTimeOffset at)
    {
        UpdatedAt = at;
        RowVersion = Guid.NewGuid().ToString("N");
    }
}