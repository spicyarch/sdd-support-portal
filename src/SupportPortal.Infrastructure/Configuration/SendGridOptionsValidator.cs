using SupportPortal.Application.Common;

namespace SupportPortal.Infrastructure.Configuration;

public sealed class SendGridOptionsValidator
{
    public EmailDeliveryAvailability Validate(SendGridOptions options, string environmentName, DateTimeOffset checkedAt)
    {
        if (!options.Enabled)
        {
            return EmailDeliveryAvailability.Disabled(checkedAt);
        }

        var invalid = new List<string>();
        AddRequired(options.ApiKey, "SendGrid:ApiKey", invalid);
        AddText(options.SenderDisplayName, "SendGrid:SenderDisplayName", 200, invalid);
        AddEmail(options.SenderAddress, "SendGrid:SenderAddress", invalid);
        AddEmail(options.ReplyToAddress, "SendGrid:ReplyToAddress", invalid);

        var recipients = options.GlobalSupportRecipients ?? [];
        if (recipients.Count == 0)
        {
            invalid.Add("SendGrid:GlobalSupportRecipients");
        }
        else
        {
            var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var recipient in recipients)
            {
                if (!TryNormalizeEmail(recipient, out var address) || !normalized.Add(address))
                {
                    invalid.Add("SendGrid:GlobalSupportRecipients");
                    break;
                }
            }
        }

        if (!TryValidatePortalUrl(options.PublicPortalUrl, environmentName))
        {
            invalid.Add("SendGrid:PublicPortalUrl");
        }

        if (options.HttpTimeoutSeconds is < 1 or > 120)
        {
            invalid.Add("SendGrid:HttpTimeoutSeconds");
        }

        if (options.MaximumAttempts is < 1 or > 10)
        {
            invalid.Add("SendGrid:MaximumAttempts");
        }

        if (options.MinimumBackoffSeconds is < 1 or > 3600)
        {
            invalid.Add("SendGrid:MinimumBackoffSeconds");
        }

        if (options.MaximumBackoffSeconds < options.MinimumBackoffSeconds || options.MaximumBackoffSeconds > 86400)
        {
            invalid.Add("SendGrid:MaximumBackoffSeconds");
        }

        if (!StringComparer.OrdinalIgnoreCase.Equals(options.DataResidency, "Global") &&
            !StringComparer.OrdinalIgnoreCase.Equals(options.DataResidency, "Eu"))
        {
            invalid.Add("SendGrid:DataResidency");
        }

        if (options.BatchSize is < 1 or > 100)
        {
            invalid.Add("SendGrid:BatchSize");
        }

        if (options.LeaseSeconds is < 30 or > 600 || options.LeaseSeconds <= options.HttpTimeoutSeconds)
        {
            invalid.Add("SendGrid:LeaseSeconds");
        }

        return invalid.Count == 0
            ? new EmailDeliveryAvailability(EmailDeliveryState.Ready, [], checkedAt)
            : new EmailDeliveryAvailability(EmailDeliveryState.InvalidConfiguration, invalid.Distinct(StringComparer.Ordinal).ToArray(), checkedAt);
    }

    private static void AddRequired(string? value, string settingName, ICollection<string> invalid)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            invalid.Add(settingName);
        }
    }

    private static void AddText(string? value, string settingName, int maximumLength, ICollection<string> invalid)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength || value.Contains('\r') || value.Contains('\n'))
        {
            invalid.Add(settingName);
        }
    }

    private static void AddEmail(string? value, string settingName, ICollection<string> invalid)
    {
        if (!TryNormalizeEmail(value, out _))
        {
            invalid.Add(settingName);
        }
    }

    private static bool TryNormalizeEmail(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 320)
        {
            return false;
        }

        return EmailAddressRules.TryNormalize(value, out normalized);
    }

    private static bool TryValidatePortalUrl(string? value, string environmentName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttps))
        {
            return true;
        }

        return StringComparer.OrdinalIgnoreCase.Equals(environmentName, "Development") &&
            StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttp) &&
            (StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "localhost") ||
             StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "127.0.0.1"));
    }
}