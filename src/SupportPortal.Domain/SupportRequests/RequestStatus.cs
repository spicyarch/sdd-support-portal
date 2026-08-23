namespace SupportPortal.Domain.SupportRequests;

public enum RequestStatus
{
    New,
    InProgress,
    WaitingOnTeam,
    Resolved,
    Closed
}