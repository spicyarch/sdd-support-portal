namespace SupportPortal.ContractTests;

public sealed class BrandingContractTests
{
    [Fact]
    public void BrandingContractDefinesSafeAnonymousProfileAndConditionalCaching()
    {
        var contract = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "branding-email-api.yaml"));

        Assert.Contains("/branding:", contract);
        Assert.Contains("security: []", contract);
        Assert.Contains("If-None-Match", contract);
        Assert.Contains("304", contract);
        Assert.Contains("EffectiveBranding", contract);
        Assert.Contains("profileVersion", contract);
        Assert.Contains("SupportContact", contract);
        Assert.DoesNotContain("apiKey:", contract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recipientAddress", contract, StringComparison.OrdinalIgnoreCase);
    }
}
