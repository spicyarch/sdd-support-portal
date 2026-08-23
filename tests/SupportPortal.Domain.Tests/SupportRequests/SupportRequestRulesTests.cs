using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Common;
using SupportPortal.Domain.SupportRequests;

namespace SupportPortal.Domain.Tests.SupportRequests;

public sealed class SupportRequestRulesTests
{
    [Fact]
    public void TeamReplyToResolvedRequestReopensIt()
    {
        var now = DateTimeOffset.UtcNow;
        var request = CreateRequest(now);
        request.ChangeStatus(RequestStatus.InProgress, PortalRole.GlobalSupportUser, now.AddMinutes(1));
        request.ChangeStatus(RequestStatus.Resolved, PortalRole.GlobalSupportUser, now.AddMinutes(2));

        request.AddMessage(new Message(Guid.NewGuid(), request.SupportRequestId, Guid.NewGuid(), PortalRole.TeamUser, "More information", Guid.NewGuid(), now.AddMinutes(3)), now.AddMinutes(3));

        Assert.Equal(RequestStatus.New, request.Status);
        Assert.Single(request.Messages);
    }

    [Fact]
    public void ClosedRequestRejectsMessages()
    {
        var now = DateTimeOffset.UtcNow;
        var request = CreateRequest(now);
        request.ChangeStatus(RequestStatus.InProgress, PortalRole.GlobalSupportUser, now.AddMinutes(1));
        request.ChangeStatus(RequestStatus.Resolved, PortalRole.GlobalSupportUser, now.AddMinutes(2));
        request.ChangeStatus(RequestStatus.Closed, PortalRole.GlobalSupportUser, now.AddMinutes(3));

        Assert.Throws<DomainException>(() => request.AddMessage(new Message(Guid.NewGuid(), request.SupportRequestId, Guid.NewGuid(), PortalRole.TeamUser, "Not allowed", Guid.NewGuid(), now.AddMinutes(4)), now.AddMinutes(4)));
    }

    [Fact]
    public void DuplicateClientMutationIsIgnored()
    {
        var now = DateTimeOffset.UtcNow;
        var request = CreateRequest(now);
        var mutationId = Guid.NewGuid();
        var first = new Message(Guid.NewGuid(), request.SupportRequestId, Guid.NewGuid(), PortalRole.TeamUser, "One", mutationId, now.AddMinutes(1));
        var duplicate = new Message(Guid.NewGuid(), request.SupportRequestId, first.AuthorUserId, PortalRole.TeamUser, "One", mutationId, now.AddMinutes(2));

        request.AddMessage(first, now.AddMinutes(1));
        request.AddMessage(duplicate, now.AddMinutes(2));

        Assert.Single(request.Messages);
    }

    private static SupportRequest CreateRequest(DateTimeOffset createdAt) => new(
        Guid.NewGuid(),
        "SP-TEST01",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Test request",
        "Request description",
        RequestPriority.Normal,
        createdAt);
}