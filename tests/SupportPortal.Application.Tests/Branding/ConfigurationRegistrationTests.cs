using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPortal.Application.Branding;
using SupportPortal.Infrastructure.Configuration;

namespace SupportPortal.Application.Tests.Branding;

public sealed class ConfigurationRegistrationTests
{
    [Fact]
    public void CompositionRegistersOnlyRedactedBrandingValidationState()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Branding:ProductName"] = "Northwind Support",
                ["Branding:PrimaryColor"] = "#FFFFFF",
                ["Branding:LogoUrl"] = "http://remote.example/logo.png"
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddAzureConfiguration(configuration)
            .BuildServiceProvider();

        var status = provider.GetRequiredService<BrandingConfigurationStatus>();
        var brand = provider.GetRequiredService<EffectiveBrandProfile>();

        Assert.Contains("Branding:PrimaryColor", status.InvalidSettingNames);
        Assert.Contains("Branding:LogoUrl", status.InvalidSettingNames);
        Assert.DoesNotContain("#FFFFFF", string.Join(',', status.InvalidSettingNames), StringComparison.Ordinal);
        Assert.Equal("Northwind Support", brand.ProductName);
        Assert.Equal(BrandingResolver.DefaultPrimaryColor, brand.PrimaryColor);
    }
}
