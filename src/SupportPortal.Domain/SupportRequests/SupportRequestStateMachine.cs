using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.SupportRequests;

public static class SupportRequestStateMachine
{
    public static void ValidateTransition(RequestStatus current, RequestStatus next, PortalRole actorRole)
    {
        if (!RoleAssignmentPolicy.IsGlobal(actorRole))
        {
            throw new DomainException("Only global support roles can change request status.");
        }

        var allowed = (current, next) switch
        {
            (RequestStatus.New, RequestStatus.InProgress) => true,
            (RequestStatus.New, RequestStatus.WaitingOnTeam) => true,
            (RequestStatus.InProgress, RequestStatus.WaitingOnTeam) => true,
            (RequestStatus.InProgress, RequestStatus.Resolved) => true,
            (RequestStatus.WaitingOnTeam, RequestStatus.InProgress) => true,
            (RequestStatus.WaitingOnTeam, RequestStatus.Resolved) => true,
            (RequestStatus.Resolved, RequestStatus.Closed) => true,
            (RequestStatus.Closed, RequestStatus.New) => true,
            _ => false
        };

        if (!allowed)
        {
            throw new DomainException($"The request cannot move from {current} to {next}.");
        }
    }
}