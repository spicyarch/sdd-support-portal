using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportPortal.Domain.Settings;

namespace SupportPortal.Infrastructure.Persistence.Configurations;

public sealed class DeploymentSettingsRecipientConfiguration : IEntityTypeConfiguration<DeploymentSettingsRecipient>
{
    public void Configure(EntityTypeBuilder<DeploymentSettingsRecipient> entity)
    {
        entity.HasKey(item => item.DeploymentSettingsRecipientId);
        entity.HasIndex(item => new { item.DeploymentSettingsId, item.NormalizedAddress }).IsUnique();
        entity.Property(item => item.NormalizedAddress).HasMaxLength(320).IsRequired();
        entity.HasOne<DeploymentSettings>()
            .WithMany()
            .HasForeignKey(item => item.DeploymentSettingsId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
