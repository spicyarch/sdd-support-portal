namespace SupportPortal.ContractTests;

public sealed class SupportPortalApiContractTests
{
    [Fact]
    public void ContractDeclaresRequiredSecurityAndMutationHeaders()
    {
        var contractPath = Path.Combine(AppContext.BaseDirectory, "support-portal-api.yaml");
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("openapi: 3.1.0", contract);
        Assert.Contains("/api/v1", contract);
        Assert.Contains("EntraBearer:", contract);
        Assert.Contains("Idempotency-Key", contract);
        Assert.Contains("If-Match", contract);
        Assert.Contains("/users/{userId}/status:", contract);
        Assert.Contains("/requests/{requestId}/priority:", contract);
        Assert.Contains("/requests/{requestId}/assignment:", contract);
        Assert.Contains("/invitations:", contract);
        Assert.Contains("/invitations/accept:", contract);
        Assert.Contains("/bootstrap:", contract);
        Assert.Contains("FunctionKey:", contract);
    }

    [Fact]
    public void ContractDeclaresAllInitialRolesAndRequestStates()
    {
        var contract = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "support-portal-api.yaml"));

        foreach (var value in new[] { "GlobalAdministrator", "GlobalSupportUser", "TeamAdministrator", "TeamUser" })
        {
            Assert.Contains(value, contract);
        }

        foreach (var value in new[] { "New", "InProgress", "WaitingOnTeam", "Resolved", "Closed" })
        {
            Assert.Contains(value, contract);
        }
    }
}