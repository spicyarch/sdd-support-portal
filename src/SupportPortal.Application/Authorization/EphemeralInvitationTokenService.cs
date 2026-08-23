using System.Security.Cryptography;
using System.Text;

namespace SupportPortal.Application.Authorization;

internal sealed class EphemeralInvitationTokenService : IInvitationTokenService
{
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);

    public TimeSpan Lifetime => TimeSpan.FromHours(72);

    public string CreateToken(Guid invitationId)
    {
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(invitationId.ToByteArray()));
    }

    public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));

    public string CreateAcceptanceLink(string token) => $"http://localhost:5258/invitations/accept?token={Uri.EscapeDataString(token)}";
}
