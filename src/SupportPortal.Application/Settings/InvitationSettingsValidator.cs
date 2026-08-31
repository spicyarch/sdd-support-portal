namespace SupportPortal.Application.Settings;

public sealed class InvitationSettingsValidator
{
    public IReadOnlyList<string> Validate(string? acceptanceBaseUrl, int lifetimeHours, string environmentName)
    {
        var invalid = new List<string>();
        if (!TryValidateBaseUrl(acceptanceBaseUrl, environmentName))
        {
            invalid.Add("Portal:InvitationAcceptanceBaseUrl");
        }

        if (lifetimeHours is < 1 or > 168)
        {
            invalid.Add("Portal:InvitationLifetimeHours");
        }

        return invalid;
    }

    private static bool TryValidateBaseUrl(string? value, string environmentName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            value.Trim().Length > 2048)
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
