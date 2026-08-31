namespace SupportPortal.ContractTests.Settings;

public sealed class GlobalSettingsContractTests
{
    [Fact]
    public void SettingsContractDeclaresRedactedAdministratorReadAndWriteSurface()
    {
        var contract = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "global-admin-settings-api.yaml"));

        Assert.Contains("/settings:", contract);
        Assert.Contains("getGlobalAdministratorSettings", contract);
        Assert.Contains("replaceGlobalAdministratorSettings", contract);
        Assert.Contains("If-Match", contract);
        Assert.Contains("Idempotency-Key", contract);
        Assert.Contains("writeOnly: true", contract);
        Assert.Contains("apiKeyConfigured", contract);
        Assert.Contains("clearApiKey", contract);
        Assert.Contains("ActivationFailed", contract);
        Assert.Contains("retryState", contract);
        Assert.Contains("Scheduled", contract);
        Assert.DoesNotContain("secretVersion:", contract, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsContractIncludesAllRuntimeSafeProfilesAndExcludesHostControls()
    {
        var contract = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "global-admin-settings-api.yaml"));

        foreach (var value in new[]
        {
            "BrandingSettings",
            "InvitationSettings",
            "SendGridSettingsView",
            "SettingsResponse",
            "HostDefaults",
            "AdministratorOverride",
            "Portal:InvitationAcceptanceBaseUrl",
            "Portal:InvitationLifetimeHours",
            "SendGrid:LeaseSeconds"
        })
        {
            Assert.Contains(value, contract);
        }

        Assert.DoesNotContain("Portal:SqlConnection", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("InvitationTokenKey", contract, StringComparison.Ordinal);
    }
}
