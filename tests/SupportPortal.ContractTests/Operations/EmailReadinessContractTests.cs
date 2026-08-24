namespace SupportPortal.ContractTests;

public sealed class EmailReadinessContractTests
{
    [Fact]
    public void ReadinessContractDeclaresSafeOperatorOnlyModesAndOutcomes()
    {
        var contract = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "branding-email-api.yaml"));

        Assert.Contains("/operations/email/readiness:", contract);
        Assert.Contains("Global Administrator", contract);
        Assert.Contains("Sandbox", contract);
        Assert.Contains("Live", contract);
        Assert.Contains("confirmLiveSend", contract);
        Assert.Contains("providerHttpStatus", contract);
        Assert.Contains("invalidSettingNames", contract);
        Assert.Contains("NoProviderRequestMade", contract);
        Assert.DoesNotContain("apiKey:", contract, StringComparison.OrdinalIgnoreCase);
    }
}
