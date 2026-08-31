using Microsoft.EntityFrameworkCore;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Settings;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Persistence;

public sealed class SettingsConcurrencyTests
{
    [Fact]
    public void FailedSettingsTransactionDoesNotPersistProfileOrRecipients()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SupportPortalDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var settingsId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var context = new SupportPortalDbContext(options))
        {
            context.Database.EnsureCreated();
            var store = new EfPortalStore(context);
            var settings = new DeploymentSettings(
                settingsId,
                now,
                Guid.NewGuid(),
                "revision-1",
                new DeploymentSettingsValues(
                    "Support Portal",
                    "SP",
                    null,
                    null,
                    "#135E96",
                    "#006B54",
                    "#006B54",
                    "Support Operations",
                    "support@example.test",
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
                    SettingsApiKeyMode.Inherit,
                    null));

            Assert.Throws<InvalidOperationException>(() => store.Execute(() =>
            {
                store.AddDeploymentSettings(settings);
                store.AddDeploymentSettingsRecipient(new DeploymentSettingsRecipient(
                    Guid.NewGuid(),
                    settingsId,
                    "support@example.test",
                    now));
                throw new InvalidOperationException("abort");
            }));
        }

        using var readContext = new SupportPortalDbContext(options);
        Assert.Null(readContext.DeploymentSettings.SingleOrDefault());
        Assert.Empty(readContext.DeploymentSettingsRecipients);
    }

    [Fact]
    public void SettingsModelUsesConcurrencyTokenAndProtectedReferenceOnly()
    {
        var options = new DbContextOptionsBuilder<SupportPortalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new SupportPortalDbContext(options);
        var entity = context.Model.FindEntityType(typeof(DeploymentSettings));

        Assert.NotNull(entity);
        Assert.True(entity!.FindProperty(nameof(DeploymentSettings.RowVersion))!.IsConcurrencyToken);
        Assert.DoesNotContain(entity.GetProperties(), property =>
            property.Name.Equals("ApiKey", StringComparison.Ordinal) ||
            property.Name.Equals("ApiKeyValue", StringComparison.Ordinal));
        Assert.NotNull(entity.FindProperty(nameof(DeploymentSettings.SendGridApiKeySecretVersion)));
    }
}