using System.Security.Cryptography;
using System.Text;
using SupportPortal.Application.Authorization;
using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Infrastructure.Persistence.Bootstrap;

public sealed class ConfiguredInvitationTokenService : IInvitationTokenService
{
    private readonly AzureOptions options;
    private readonly byte[] key;

    public ConfiguredInvitationTokenService(AzureOptions options)
    {
        this.options = options;
        key = ResolveKey(options.InvitationTokenKey, options.AuthenticationMode);
    }

    public TimeSpan Lifetime => TimeSpan.FromHours(Math.Clamp(options.InvitationLifetimeHours, 1, 168));

    public string CreateToken(Guid invitationId)
    {
        EnsureKey();
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(invitationId.ToByteArray()));
    }

    public string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
    }

    public string CreateAcceptanceLink(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (string.IsNullOrWhiteSpace(options.InvitationAcceptanceBaseUrl))
        {
            throw new InvalidOperationException("Portal:InvitationAcceptanceBaseUrl is required to create an invitation.");
        }

        return $"{options.InvitationAcceptanceBaseUrl.TrimEnd('/')}?token={Uri.EscapeDataString(token)}";
    }

    private void EnsureKey()
    {
        if (key.Length < 32)
        {
            throw new InvalidOperationException("Portal:InvitationTokenKey must contain at least 32 bytes outside Development mode.");
        }
    }

    private static byte[] ResolveKey(string? configuredKey, string authenticationMode)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return StringComparer.OrdinalIgnoreCase.Equals(authenticationMode, "Development")
                ? RandomNumberGenerator.GetBytes(32)
                : [];
        }

        try
        {
            var decoded = Convert.FromBase64String(configuredKey);
            return decoded.Length >= 32 ? decoded : Encoding.UTF8.GetBytes(configuredKey);
        }
        catch (FormatException)
        {
            return Encoding.UTF8.GetBytes(configuredKey);
        }
    }
}
