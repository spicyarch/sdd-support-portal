using Microsoft.EntityFrameworkCore;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.Settings;
using SupportPortal.Infrastructure.Persistence;

namespace SupportPortal.Api.IntegrationTests.Persistence;

public sealed class SettingsPersistenceTests
{
    [Fact]
    public void PersistsOneDeploymentProfileAndRecipientsAcrossContextsWithoutApiKeyColumn()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SupportPortalDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var userId = Guid.NewGuid();
        var settingsId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var values = new DeploymentSettingsValues(
            "Northwind Support",
            "NS",
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
            "Northwind Support",
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
            null);

        using (var writeContext = new SupportPortalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            var store = new EfPortalStore(writeContext);
            store.Execute(() =>
            {
                store.AddUser(new User(userId, Guid.NewGuid(), Guid.NewGuid(), "Global Administrator", "admin@example.test", now));
                store.AddDeploymentSettings(new DeploymentSettings(settingsId, now, userId, "revision-1", values));
                store.AddDeploymentSettingsRecipient(new DeploymentSettingsRecipient(Guid.NewGuid(), settingsId, "support@example.test", now));
            });
        }

        using var readContext = new SupportPortalDbContext(options);
        var readStore = new EfPortalStore(readContext);
        var persisted = readStore.GetDeploymentSettings();
        var recipients = readStore.GetDeploymentSettingsRecipients(settingsId);
        var propertyNames = readContext.Model.FindEntityType(typeof(DeploymentSettings))!
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.NotNull(persisted);
        Assert.Equal("Northwind Support", persisted!.ProductName);
        Assert.Equal("revision-1", persisted.Revision);
        Assert.Single(recipients);
        Assert.Equal("support@example.test", recipients[0].NormalizedAddress);
        Assert.DoesNotContain("ApiKey", propertyNames);
        Assert.Contains("SendGridApiKeySecretVersion", propertyNames);
    }
}
