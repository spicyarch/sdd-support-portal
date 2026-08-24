using System.Security.Cryptography;
using System.Text;
using SupportPortal.Application.Common;

namespace SupportPortal.Application.Branding;

public static class BrandingResolver
{
    public const string DefaultProductName = "Support Portal";
    public const string DefaultShortProductName = "SP";
    public const string DefaultPrimaryColor = "#135E96";
    public const string DefaultAccentColor = "#006B54";
    public const string DefaultFocusColor = "#006B54";
    public const string DefaultSupportContactName = "Support Operations";
    public const string DefaultSupportContactEmail = "support@example.com";

    public static EffectiveBrandProfile Resolve(BrandingInput input, string environmentName)
    {
        var productName = ResolveText(input.ProductName, DefaultProductName, 100);
        var shortProductName = ResolveText(input.ShortProductName, string.Empty, 20, required: false);
        var initials = string.IsNullOrWhiteSpace(shortProductName)
            ? DeriveInitials(productName)
            : NormalizeCompactName(shortProductName);
        if (string.IsNullOrWhiteSpace(initials))
        {
            initials = DeriveInitials(productName);
        }
        var logoUrl = ResolveImageUrl(input.LogoUrl, environmentName);
        var faviconUrl = ResolveImageUrl(input.FaviconUrl, environmentName);
        var primaryColor = ResolveColor(input.PrimaryColor, DefaultPrimaryColor, color => BrandContrastValidator.MeetsTextContrast(color));
        var accentColor = ResolveColor(input.AccentColor, DefaultAccentColor, color => BrandContrastValidator.MeetsTextContrast(color));
        var focusColor = ResolveColor(input.FocusColor, DefaultFocusColor, BrandContrastValidator.MeetsFocusContrast);
        var supportContactName = ResolveText(input.SupportContactName, DefaultSupportContactName, 200);
        var supportContactEmail = ResolveEmail(input.SupportContactEmail, DefaultSupportContactEmail);
        var organizationName = ResolveText(input.OrganizationName, string.Empty, 200, required: false);
        var profileVersion = CreateProfileVersion(
            productName,
            shortProductName,
            initials,
            logoUrl,
            faviconUrl,
            primaryColor,
            accentColor,
            focusColor,
            supportContactName,
            supportContactEmail,
            organizationName);

        return new EffectiveBrandProfile(
            productName,
            string.IsNullOrWhiteSpace(shortProductName) ? initials : shortProductName,
            initials,
            logoUrl,
            faviconUrl,
            primaryColor,
            accentColor,
            focusColor,
            supportContactName,
            supportContactEmail,
            string.IsNullOrWhiteSpace(organizationName) ? null : organizationName,
            profileVersion);
    }

    private static string ResolveText(string? value, string fallback, int maximumLength, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return required ? fallback : string.Empty;
        }

        var normalized = value.Trim();
        return normalized.Length <= maximumLength && !normalized.Contains('\r') && !normalized.Contains('\n')
            ? normalized
            : fallback;
    }

    private static string ResolveColor(string? value, string fallback, Func<string, bool> validator) =>
        BrandContrastValidator.IsOpaqueHexColor(value) && validator(value!) ? value!.ToUpperInvariant() : fallback;

    private static string? ResolveImageUrl(string? value, string environmentName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            value.Trim().Length > 2048)
        {
            return null;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttps))
        {
            return uri.ToString();
        }

        return StringComparer.OrdinalIgnoreCase.Equals(environmentName, "Development") &&
            StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttp) &&
            (StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "localhost") ||
             StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "127.0.0.1"))
            ? uri.ToString()
            : null;
    }

    private static string ResolveEmail(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 320)
        {
            return fallback;
        }

        return EmailAddressRules.TryNormalize(value, out var normalized) ? normalized : fallback;
    }

    private static string DeriveInitials(string value)
    {
        var words = value.Split([' ', '\t', '-', '_', '.', '/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var initials = new string(words
            .Take(3)
            .Select(word => word.FirstOrDefault(char.IsLetterOrDigit))
            .Where(character => character != default)
            .ToArray())
            .ToUpperInvariant();
        return string.IsNullOrWhiteSpace(initials) ? DefaultShortProductName : initials;
    }

    private static string NormalizeCompactName(string value) =>
        new string(value.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();

    private static string CreateProfileVersion(params string?[] values)
    {
        var canonical = string.Join('|', values.Select(value => value ?? string.Empty));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}