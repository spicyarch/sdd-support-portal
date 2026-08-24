using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Application.Tests.Branding;

public sealed class BrandingOptionsValidatorTests
{
    [Fact]
    public void MissingOptionalBrandingValuesUseResolverFallbacksWithoutInvalidSettings()
    {
        var result = new BrandingOptionsValidator().Validate(new BrandingOptions(), "Production");

        Assert.Empty(result);
    }

    [Fact]
    public void SuppliedUnsafeValuesAreReportedBySettingNameOnly()
    {
        const string unsafeName = "<script>invalid</script>";
        var result = new BrandingOptionsValidator().Validate(new BrandingOptions
        {
            ProductName = new string('x', 101),
            LogoUrl = "http://remote.example/logo.png",
            PrimaryColor = "#FFFFFF",
            AccentColor = "not-a-color",
            FocusColor = "#FFFFFF",
            SupportContactEmail = unsafeName
        }, "Production");

        Assert.Contains("Branding:ProductName", result);
        Assert.Contains("Branding:LogoUrl", result);
        Assert.Contains("Branding:PrimaryColor", result);
        Assert.Contains("Branding:AccentColor", result);
        Assert.Contains("Branding:FocusColor", result);
        Assert.Contains("Branding:SupportContactEmail", result);
        Assert.DoesNotContain(unsafeName, string.Join(',', result), StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentAllowsLoopbackHttpImagesButProductionDoesNot()
    {
        var options = new BrandingOptions
        {
            LogoUrl = "http://localhost:5258/logo.png",
            FaviconUrl = "http://127.0.0.1/favicon.png"
        };

        Assert.Empty(new BrandingOptionsValidator().Validate(options, "Development"));
        Assert.Contains("Branding:LogoUrl", new BrandingOptionsValidator().Validate(options, "Production"));
        Assert.Contains("Branding:FaviconUrl", new BrandingOptionsValidator().Validate(options, "Production"));
    }
}
