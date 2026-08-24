using SupportPortal.Application.Branding;

namespace SupportPortal.Application.Tests.Branding;

public sealed class BrandingResolverTests
{
    [Fact]
    public void MissingValuesUseAccessibleDefaultsAndReadableInitials()
    {
        var profile = BrandingResolver.Resolve(new BrandingInput(null, null, null, null, null, null, null, null, null, null), "Production");

        Assert.Equal("Support Portal", profile.ProductName);
        Assert.Equal("SP", profile.ShortProductName);
        Assert.Equal("SP", profile.Initials);
        Assert.Equal(BrandingResolver.DefaultPrimaryColor, profile.PrimaryColor);
        Assert.Equal(BrandingResolver.DefaultAccentColor, profile.AccentColor);
        Assert.Equal(BrandingResolver.DefaultFocusColor, profile.FocusColor);
        Assert.Null(profile.LogoUrl);
        Assert.Null(profile.OrganizationName);
        Assert.True(BrandContrastValidator.MeetsTextContrast(profile.PrimaryColor));
        Assert.True(BrandContrastValidator.MeetsFocusContrast(profile.FocusColor));
    }

    [Fact]
    public void InvalidColorsAndUnsafeImagesFallBackIndependently()
    {
        var profile = BrandingResolver.Resolve(
            new BrandingInput(
                "Northwind Support",
                "NS",
                "http://external.example/logo.png",
                "javascript:alert(1)",
                "#FFFFFF",
                "#006B54",
                "not-a-color",
                "Operations",
                "ops@example.test",
                "Northwind"),
            "Production");

        Assert.Equal("Northwind Support", profile.ProductName);
        Assert.Equal("NS", profile.Initials);
        Assert.Null(profile.LogoUrl);
        Assert.Null(profile.FaviconUrl);
        Assert.Equal(BrandingResolver.DefaultPrimaryColor, profile.PrimaryColor);
        Assert.Equal(BrandingResolver.DefaultFocusColor, profile.FocusColor);
        Assert.Equal("#006B54", profile.AccentColor);
    }

    [Fact]
    public void DevelopmentAllowsOnlyLoopbackHttpImages()
    {
        var profile = BrandingResolver.Resolve(
            new BrandingInput("Portal", "P", "http://localhost:5258/logo.png", "http://127.0.0.1/favicon.png", null, null, null, null, null, null),
            "Development");

        Assert.Equal("http://localhost:5258/logo.png", profile.LogoUrl);
        Assert.Equal("http://127.0.0.1/favicon.png", profile.FaviconUrl);
    }
}