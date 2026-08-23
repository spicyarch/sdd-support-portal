namespace SupportPortal.Application.Authorization;

public interface IInvitationTokenService
{
    TimeSpan Lifetime { get; }

    string CreateToken(Guid invitationId);

    string HashToken(string token);

    string CreateAcceptanceLink(string token);
}
