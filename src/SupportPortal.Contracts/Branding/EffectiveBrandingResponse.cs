namespace SupportPortal.Contracts.Branding;

public sealed record EffectiveBrandingResponse(
    string ProductName,
    string ShortProductName,
    string Initials,
    string? LogoUrl,
    string? FaviconUrl,
    string PrimaryColor,
    string AccentColor,
    string FocusColor,
    SupportContactResponse SupportContact,
    string? OrganizationName,
    string ProfileVersion);