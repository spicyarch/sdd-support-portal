using SupportPortal.Infrastructure.Configuration;
using SupportPortal.Infrastructure.Persistence.Bootstrap;

namespace SupportPortal.Api.IntegrationTests.Security;

public sealed class InvitationTokenConfigurationTests
{
    [Fact]
    public void ProductionInvitationSigningRejectsShortKeys()
    {
        var service = new ConfiguredInvitationTokenService(new AzureOptions
        {
            AuthenticationMode = "Entra",
            InvitationTokenKey = "too-short"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => service.CreateToken(Guid.NewGuid()));

        Assert.Contains("32 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentInvitationSigningUsesAnEphemeralKeyWhenUnset()
    {
        var service = new ConfiguredInvitationTokenService(new AzureOptions
        {
            AuthenticationMode = "Development"
        });

        var token = service.CreateToken(Guid.NewGuid());

        Assert.False(string.IsNullOrWhiteSpace(token));
    }
}
