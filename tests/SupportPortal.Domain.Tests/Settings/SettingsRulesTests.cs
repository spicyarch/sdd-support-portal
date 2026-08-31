using SupportPortal.Domain.Common;
using SupportPortal.Domain.Settings;

namespace SupportPortal.Domain.Tests.Settings;

public sealed class SettingsRulesTests
{
    [Fact]
    public void ManagedApiKeyRequiresAProtectedSecretReference()
    {
        var exception = Assert.Throws<DomainException>(() => new DeploymentSettings(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "revision-1",
            Values(SettingsApiKeyMode.Managed, null)));

        Assert.Contains("secret version", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InheritedAndClearedApiKeysCannotCarryASecretReference()
    {
        var exception = Assert.Throws<DomainException>(() => new DeploymentSettings(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "revision-1",
            Values(SettingsApiKeyMode.Cleared, "version-1")));

        Assert.Contains("managed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReplacingValuesChangesRevisionMetadataAndRowVersion()
    {
        var settings = new DeploymentSettings(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "revision-1",
            Values(SettingsApiKeyMode.Inherit, null));
        var originalRowVersion = settings.RowVersion;
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(1);

        settings.Replace(Values(SettingsApiKeyMode.Managed, "version-2"), "revision-2", updatedAt, Guid.NewGuid());

        Assert.Equal("revision-2", settings.Revision);
        Assert.Equal(updatedAt, settings.UpdatedAt);
        Assert.NotEqual(originalRowVersion, settings.RowVersion);
        Assert.Equal(SettingsApiKeyMode.Managed, settings.SendGridApiKeyMode);
    }

    private static DeploymentSettingsValues Values(SettingsApiKeyMode mode, string? version) => new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        "https://portal.example.test/invitations/accept",
        72,
        false,
        "Support Portal",
        "sender@example.test",
        "support@example.test",
        "https://portal.example.test",
        15,
        4,
        5,
        60,
        "Global",
        25,
        60,
        mode,
        version);
}
