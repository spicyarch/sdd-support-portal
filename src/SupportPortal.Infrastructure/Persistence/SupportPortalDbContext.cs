using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SupportPortal.Domain.Auditing;
using SupportPortal.Domain.Authorization;
using SupportPortal.Domain.SupportRequests;
using SupportPortal.Domain.Teams;
using SupportPortal.Domain.Notifications;

namespace SupportPortal.Infrastructure.Persistence;

public sealed class SupportPortalDbContext(DbContextOptions<SupportPortalDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<SupportRequest> SupportRequests => Set<SupportRequest>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<CommandReceipt> CommandReceipts => Set<CommandReceipt>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    public DbSet<NotificationAttempt> NotificationAttempts => Set<NotificationAttempt>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<NullableUtcDateTimeOffsetConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(item => item.UserId);
            entity.HasIndex(item => new { item.TenantId, item.ObjectId }).IsUnique();
            entity.Property(item => item.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(320).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.RowVersion).IsConcurrencyToken().HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(item => item.TeamId);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.RowVersion).IsConcurrencyToken().HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<RoleAssignment>(entity =>
        {
            entity.HasKey(item => item.RoleAssignmentId);
            entity.HasIndex(item => item.UserId)
                .IsUnique()
                .HasFilter("[RevokedAt] IS NULL");
            entity.Property(item => item.Role).HasConversion<string>().HasMaxLength(48);
            entity.Property(item => item.RevocationReason).HasMaxLength(500);
            entity.Property(item => item.RowVersion).IsConcurrencyToken().HasMaxLength(64).IsRequired();
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Team>().WithMany().HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.HasKey(item => item.InvitationId);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.Property(item => item.Email).HasMaxLength(320).IsRequired();
            entity.Property(item => item.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Role).HasConversion<string>().HasMaxLength(48);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.AcceptedAt);
            entity.Property(item => item.RevokedByUserId);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Team>().WithMany().HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupportRequest>(entity =>
        {
            entity.HasKey(item => item.SupportRequestId);
            entity.HasIndex(item => item.Reference).IsUnique();
            entity.HasIndex(item => new { item.TeamId, item.Status, item.Priority });
            entity.Property(item => item.Subject).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(10000).IsRequired();
            entity.Property(item => item.Priority).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.RowVersion).IsConcurrencyToken().HasMaxLength(64).IsRequired();
            entity.HasOne<Team>().WithMany().HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.Messages)
                .WithOne()
                .HasForeignKey(item => item.SupportRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(item => item.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(item => item.MessageId);
            entity.HasIndex(item => new { item.SupportRequestId, item.AuthorUserId, item.ClientMutationId }).IsUnique();
            entity.Property(item => item.AuthorRole).HasConversion<string>().HasMaxLength(48);
            entity.Property(item => item.Body).HasMaxLength(10000).IsRequired();
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(item => item.AuditEventId);
            entity.HasIndex(item => new { item.TargetType, item.TargetId, item.OccurredAt });
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.TargetType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Metadata).HasMaxLength(4000);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CommandReceipt>(entity =>
        {
            entity.HasKey(item => item.CommandReceiptId);
            entity.HasIndex(item => new { item.ActorUserId, item.IdempotencyKey }).IsUnique();
            entity.HasIndex(item => item.CreatedAt);
            entity.Property(item => item.RequestFingerprint).HasMaxLength(128).IsRequired();
            entity.Property(item => item.ResponseBody).IsRequired();
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(item => item.NotificationId);
            entity.HasIndex(item => new { item.EventType, item.SourceEntityId }).IsUnique();
            entity.HasIndex(item => new { item.Status, item.CreatedAt });
            entity.Property(item => item.EventType).HasConversion<string>().HasMaxLength(48).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(48).IsRequired();
            entity.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.RowVersion).IsConcurrencyToken().HasMaxLength(64).IsRequired();
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.AssigneeUserIdAtEvent).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupportRequest>().WithMany().HasForeignKey(item => item.SupportRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Invitation>().WithMany().HasForeignKey(item => item.InvitationId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Notifications_SourceContext",
                "(([EventType] = N'InvitationCreated' AND [InvitationId] IS NOT NULL AND [SupportRequestId] IS NULL) OR ([EventType] <> N'InvitationCreated' AND [InvitationId] IS NULL AND [SupportRequestId] IS NOT NULL))"));
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.HasKey(item => item.NotificationDeliveryId);
            entity.HasIndex(item => new { item.NotificationId, item.RecipientKey }).IsUnique();
            entity.HasIndex(item => new { item.State, item.NextAttemptAt, item.LeaseExpiresAt });
            entity.Property(item => item.RecipientKind).HasConversion<string>().HasMaxLength(48).IsRequired();
            entity.Property(item => item.RecipientAddress).HasMaxLength(320);
            entity.Property(item => item.RecipientKey).HasMaxLength(128).IsRequired();
            entity.Property(item => item.State).HasConversion<string>().HasMaxLength(48).IsRequired();
            entity.Property(item => item.LastFailureCategory).HasConversion<string>().HasMaxLength(64).IsRequired();
            entity.Property(item => item.LeaseOwner).HasMaxLength(128);
            entity.Property(item => item.ProviderMessageId).HasMaxLength(256);
            entity.Property(item => item.RowVersion).IsConcurrencyToken().HasMaxLength(64).IsRequired();
            entity.HasOne<Notification>().WithMany().HasForeignKey(item => item.NotificationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_NotificationDeliveries_RecipientDetails",
                "(([RecipientKind] = N'PortalUser' AND [RecipientUserId] IS NOT NULL AND [RecipientAddress] IS NULL) OR ([RecipientKind] = N'ConfiguredGlobalMailbox' AND [RecipientUserId] IS NULL AND [RecipientAddress] IS NOT NULL) OR ([RecipientKind] = N'InvitationRecipient' AND [RecipientUserId] IS NULL AND [RecipientAddress] IS NULL))"));
        });

        modelBuilder.Entity<NotificationAttempt>(entity =>
        {
            entity.HasKey(item => item.NotificationAttemptId);
            entity.HasIndex(item => new { item.NotificationDeliveryId, item.AttemptNumber }).IsUnique();
            entity.HasIndex(item => new { item.Outcome, item.CompletedAt });
            entity.Property(item => item.Outcome).HasConversion<string>().HasMaxLength(48).IsRequired();
            entity.Property(item => item.FailureCategory).HasConversion<string>().HasMaxLength(64).IsRequired();
            entity.Property(item => item.ProviderMessageId).HasMaxLength(256);
            entity.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
            entity.HasOne<NotificationDelivery>().WithMany().HasForeignKey(item => item.NotificationDeliveryId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(value => value.ToUniversalTime(), value => value.ToUniversalTime())
    {
    }
}

public sealed class NullableUtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset?, DateTimeOffset?>
{
    public NullableUtcDateTimeOffsetConverter()
        : base(value => value.HasValue ? value.Value.ToUniversalTime() : value, value => value.HasValue ? value.Value.ToUniversalTime() : value)
    {
    }
}