using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportPortal.Domain.Settings;

namespace SupportPortal.Infrastructure.Persistence.Configurations;

public sealed class DeploymentSettingsConfiguration : IEntityTypeConfiguration<DeploymentSettings>
{
    public void Configure(EntityTypeBuilder<DeploymentSettings> entity)
    {
        entity.HasKey(item => item.DeploymentSettingsId);
        entity.HasIndex(item => item.ScopeKey).IsUnique();
        entity.HasIndex(item => item.Revision).IsUnique();
        entity.Property(item => item.ScopeKey).HasMaxLength(32).IsRequired();
        entity.Property(item => item.Revision).HasMaxLength(160).IsRequired();
        entity.Property(item => item.RowVersion).HasMaxLength(64).IsRequired().IsConcurrencyToken();
        entity.Property(item => item.UpdatedByUserId);
        entity.Property(item => item.ProductName).HasMaxLength(100);
        entity.Property(item => item.ShortProductName).HasMaxLength(20);
        entity.Property(item => item.LogoUrl).HasMaxLength(2048);
        entity.Property(item => item.FaviconUrl).HasMaxLength(2048);
        entity.Property(item => item.PrimaryColor).HasMaxLength(7);
        entity.Property(item => item.AccentColor).HasMaxLength(7);
        entity.Property(item => item.FocusColor).HasMaxLength(7);
        entity.Property(item => item.SupportContactName).HasMaxLength(200);
        entity.Property(item => item.SupportContactEmail).HasMaxLength(320);
        entity.Property(item => item.OrganizationName).HasMaxLength(200);
        entity.Property(item => item.InvitationAcceptanceBaseUrl).HasMaxLength(2048);
        entity.Property(item => item.SendGridSenderDisplayName).HasMaxLength(200);
        entity.Property(item => item.SendGridSenderAddress).HasMaxLength(320);
        entity.Property(item => item.SendGridReplyToAddress).HasMaxLength(320);
        entity.Property(item => item.SendGridPublicPortalUrl).HasMaxLength(2048);
        entity.Property(item => item.SendGridDataResidency).HasMaxLength(16);
        entity.Property(item => item.SendGridApiKeyMode).HasConversion<string>().HasMaxLength(16).IsRequired();
        entity.Property(item => item.SendGridApiKeySecretVersion).HasMaxLength(256);
        entity.HasOne<SupportPortal.Domain.Authorization.User>()
            .WithMany()
            .HasForeignKey(item => item.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table => table.HasCheckConstraint(
            "CK_DeploymentSettings_Scope",
            "[ScopeKey] = N'global'"));
        entity.ToTable(table => table.HasCheckConstraint(
            "CK_DeploymentSettings_ApiKeyReference",
            "([SendGridApiKeyMode] = N'Managed' AND [SendGridApiKeySecretVersion] IS NOT NULL) OR ([SendGridApiKeyMode] <> N'Managed' AND [SendGridApiKeySecretVersion] IS NULL)"));
    }
}
