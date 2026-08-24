using System.Net;
using SupportPortal.Application.Common;
using SupportPortal.Application.Branding;

namespace SupportPortal.Infrastructure.Configuration;

public sealed record BrandingConfigurationStatus(
    IReadOnlyList<string> InvalidSettingNames,
    DateTimeOffset CheckedAt)
{
    public bool UsesFallbacks => InvalidSettingNames.Count > 0;
}

public sealed class BrandingOptionsValidator
{
    public IReadOnlyList<string> Validate(BrandingOptions options, string environmentName)
    {
        var invalid = new List<string>();
        ValidateText(options.ProductName, "Branding:ProductName", 100, invalid, required: false);
        ValidateText(options.ShortProductName, "Branding:ShortProductName", 20, invalid, required: false);
        ValidateImageUrl(options.LogoUrl, "Branding:LogoUrl", environmentName, invalid);
        ValidateImageUrl(options.FaviconUrl, "Branding:FaviconUrl", environmentName, invalid);
        ValidateColor(options.PrimaryColor, "Branding:PrimaryColor", color => BrandContrastValidator.MeetsTextContrast(color), invalid);
        ValidateColor(options.AccentColor, "Branding:AccentColor", color => BrandContrastValidator.MeetsTextContrast(color), invalid);
        ValidateColor(options.FocusColor, "Branding:FocusColor", BrandContrastValidator.MeetsFocusContrast, invalid);
        ValidateText(options.SupportContactName, "Branding:SupportContactName", 200, invalid, required: false);
        ValidateEmail(options.SupportContactEmail, "Branding:SupportContactEmail", invalid);
        ValidateText(options.OrganizationName, "Branding:OrganizationName", 200, invalid, required: false);
        return invalid;
    }

    private static void ValidateText(string? value, string settingName, int maximumLength, ICollection<string> invalid, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                invalid.Add(settingName);
            }

            return;
        }

        if (value.Trim().Length > maximumLength || value.Contains('\r') || value.Contains('\n'))
        {
            invalid.Add(settingName);
        }
    }

    private static void ValidateImageUrl(string? value, string settingName, string environmentName, ICollection<string> invalid)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

                if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            !StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttps) &&
            !(StringComparer.OrdinalIgnoreCase.Equals(environmentName, "Development") &&
              StringComparer.OrdinalIgnoreCase.Equals(uri.Scheme, Uri.UriSchemeHttp) &&
                            (StringComparer.OrdinalIgnoreCase.Equals(uri.Host, "localhost") ||
                             IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address))) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
                        value.Trim().Length > 2048)
        {
            invalid.Add(settingName);
        }
    }

    private static void ValidateColor(string? value, string settingName, Func<string, bool> contrastValidator, ICollection<string> invalid)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!BrandContrastValidator.IsOpaqueHexColor(value) || !contrastValidator(value))
        {
            invalid.Add(settingName);
        }
    }

    private static void ValidateEmail(string? value, string settingName, ICollection<string> invalid)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.Length > 320)
        {
            invalid.Add(settingName);
            return;
        }

        if (!EmailAddressRules.TryNormalize(value, out _))
        {
            invalid.Add(settingName);
        }
    }
}