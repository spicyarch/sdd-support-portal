using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SupportPortal.Application.Abstractions;
using SupportPortal.Application.Common;
using SupportPortal.Contracts.Settings;
using SupportPortal.Domain.Auditing;

namespace SupportPortal.Application.Commands;

public sealed class IdempotencyService
{
    private readonly IPortalStore store;

    public IdempotencyService(IPortalStore store)
    {
        this.store = store;
    }

    public bool TryReplay<T>(Guid actorUserId, Guid idempotencyKey, string fingerprint, out T? response)
    {
        var receipt = store.GetCommandReceipt(actorUserId, idempotencyKey);
        if (receipt is null)
        {
            response = default;
            return false;
        }

        if (!StringComparer.Ordinal.Equals(receipt.RequestFingerprint, fingerprint))
        {
            throw new PortalServiceException(409, "Idempotency conflict", "The idempotency key was already used for a different mutation.");
        }

        response = JsonSerializer.Deserialize<T>(receipt.ResponseBody);
        return true;
    }

    public CommandReceipt CreateReceipt<T>(Guid actorUserId, Guid idempotencyKey, string fingerprint, int statusCode, T response, DateTimeOffset now)
    {
        return new CommandReceipt(
            Guid.NewGuid(),
            actorUserId,
            idempotencyKey,
            fingerprint,
            statusCode,
            JsonSerializer.Serialize(response),
            now);
    }

    public static string Fingerprint(string operation, object request)
    {
        var payload = Encoding.UTF8.GetBytes($"{operation}:{JsonSerializer.Serialize(request)}");
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    public static string FingerprintSettings(UpdateGlobalSettingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var safeRequest = new
        {
            request.Branding,
            request.Invitation,
            SendGrid = new
            {
                request.SendGrid.Enabled,
                request.SendGrid.SenderDisplayName,
                request.SendGrid.SenderAddress,
                request.SendGrid.ReplyToAddress,
                request.SendGrid.GlobalSupportRecipients,
                request.SendGrid.PublicPortalUrl,
                request.SendGrid.HttpTimeoutSeconds,
                request.SendGrid.MaximumAttempts,
                request.SendGrid.MinimumBackoffSeconds,
                request.SendGrid.MaximumBackoffSeconds,
                request.SendGrid.DataResidency,
                request.SendGrid.BatchSize,
                request.SendGrid.LeaseSeconds,
                ApiKeySupplied = !string.IsNullOrWhiteSpace(request.SendGrid.ApiKey),
                request.SendGrid.ClearApiKey
            }
        };
        return Fingerprint("replace-settings", safeRequest);
    }
}