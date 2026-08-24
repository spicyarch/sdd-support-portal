using SupportPortal.Contracts.Branding;

namespace SupportPortal.Application.Branding;

public sealed record BrandingInput(
    string? ProductName,
    string? ShortProductName,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? AccentColor,
    string? FocusColor,
    string? SupportContactName,
    string? SupportContactEmail,
    string? OrganizationName);

public sealed record EffectiveBrandProfile(
    string ProductName,
    string ShortProductName,
    string Initials,
    string? LogoUrl,
    string? FaviconUrl,
    string PrimaryColor,
    string AccentColor,
    string FocusColor,
    string SupportContactName,
    string SupportContactEmail,
    string? OrganizationName,
    string ProfileVersion)
{
    public EffectiveBrandingResponse ToResponse() => new(
        ProductName,
        ShortProductName,
        Initials,
        LogoUrl,
        FaviconUrl,
        PrimaryColor,
        AccentColor,
        FocusColor,
        new SupportContactResponse(SupportContactName, SupportContactEmail),
        OrganizationName,
        ProfileVersion);
}