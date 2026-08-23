using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.SupportRequests;

public sealed class Message
{
    public Message(
        Guid messageId,
        Guid supportRequestId,
        Guid authorUserId,
        PortalRole authorRole,
        string body,
        Guid clientMutationId,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainException("Message body is required.");
        }

        MessageId = messageId;
        SupportRequestId = supportRequestId;
        AuthorUserId = authorUserId;
        AuthorRole = authorRole;
        Body = body.Trim();
        ClientMutationId = clientMutationId;
        CreatedAt = createdAt;
    }

    public Guid MessageId { get; private set; }

    public Guid SupportRequestId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public PortalRole AuthorRole { get; private set; }

    public string Body { get; private set; }

    public Guid ClientMutationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}