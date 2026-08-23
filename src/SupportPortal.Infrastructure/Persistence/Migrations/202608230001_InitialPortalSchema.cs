using Microsoft.EntityFrameworkCore.Migrations;

namespace SupportPortal.Infrastructure.Persistence.Migrations;

public partial class InitialPortalSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Teams",
            columns: table => new
            {
                TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DeactivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Teams", x => x.TeamId);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DeactivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.UserId);
            });

        migrationBuilder.CreateTable(
            name: "RoleAssignments",
            columns: table => new
            {
                RoleAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Role = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RevocationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleAssignments", x => x.RoleAssignmentId);
                table.ForeignKey("FK_RoleAssignments_Teams_TeamId", x => x.TeamId, "Teams", "TeamId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_RoleAssignments_Users_AssignedByUserId", x => x.AssignedByUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_RoleAssignments_Users_RevokedByUserId", x => x.RevokedByUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_RoleAssignments_Users_UserId", x => x.UserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Invitations",
            columns: table => new
            {
                InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                Role = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invitations", x => x.InvitationId);
                table.ForeignKey("FK_Invitations_Teams_TeamId", x => x.TeamId, "Teams", "TeamId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Invitations_Users_CreatedByUserId", x => x.CreatedByUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Invitations_Users_RevokedByUserId", x => x.RevokedByUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SupportRequests",
            columns: table => new
            {
                SupportRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Priority = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportRequests", x => x.SupportRequestId);
                table.ForeignKey("FK_SupportRequests_Teams_TeamId", x => x.TeamId, "Teams", "TeamId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SupportRequests_Users_AssignedToUserId", x => x.AssignedToUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SupportRequests_Users_CreatedByUserId", x => x.CreatedByUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AuditEvents",
            columns: table => new
            {
                AuditEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TargetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Succeeded = table.Column<bool>(type: "bit", nullable: false),
                Metadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEvents", x => x.AuditEventId);
                table.ForeignKey("FK_AuditEvents_Users_ActorUserId", x => x.ActorUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Messages",
            columns: table => new
            {
                MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SupportRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuthorRole = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ClientMutationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Messages", x => x.MessageId);
                table.ForeignKey("FK_Messages_SupportRequests_SupportRequestId", x => x.SupportRequestId, "SupportRequests", "SupportRequestId", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_Messages_Users_AuthorUserId", x => x.AuthorUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CommandReceipts",
            columns: table => new
            {
                CommandReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                ResponseStatus = table.Column<int>(type: "int", nullable: false),
                ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CommandReceipts", x => x.CommandReceiptId);
                table.ForeignKey("FK_CommandReceipts_Users_ActorUserId", x => x.ActorUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_Teams_Name", table: "Teams", column: "Name", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Users_TenantId_ObjectId", table: "Users", columns: new[] { "TenantId", "ObjectId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_RoleAssignments_TeamId", table: "RoleAssignments", column: "TeamId");
        migrationBuilder.CreateIndex(name: "IX_RoleAssignments_UserId", table: "RoleAssignments", column: "UserId");
        migrationBuilder.CreateIndex(name: "IX_RoleAssignments_UserId_Active", table: "RoleAssignments", column: "UserId", unique: true, filter: "[RevokedAt] IS NULL");
        migrationBuilder.CreateIndex(name: "IX_Invitations_TeamId", table: "Invitations", column: "TeamId");
        migrationBuilder.CreateIndex(name: "IX_Invitations_TokenHash", table: "Invitations", column: "TokenHash", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Invitations_CreatedByUserId", table: "Invitations", column: "CreatedByUserId");
        migrationBuilder.CreateIndex(name: "IX_Invitations_RevokedByUserId", table: "Invitations", column: "RevokedByUserId");
        migrationBuilder.CreateIndex(name: "IX_SupportRequests_Reference", table: "SupportRequests", column: "Reference", unique: true);
        migrationBuilder.CreateIndex(name: "IX_SupportRequests_TeamId_Status_Priority", table: "SupportRequests", columns: new[] { "TeamId", "Status", "Priority" });
        migrationBuilder.CreateIndex(name: "IX_SupportRequests_AssignedToUserId", table: "SupportRequests", column: "AssignedToUserId");
        migrationBuilder.CreateIndex(name: "IX_SupportRequests_CreatedByUserId", table: "SupportRequests", column: "CreatedByUserId");
        migrationBuilder.CreateIndex(name: "IX_AuditEvents_TargetType_TargetId_OccurredAt", table: "AuditEvents", columns: new[] { "TargetType", "TargetId", "OccurredAt" });
        migrationBuilder.CreateIndex(name: "IX_AuditEvents_ActorUserId", table: "AuditEvents", column: "ActorUserId");
        migrationBuilder.CreateIndex(name: "IX_Messages_SupportRequestId_AuthorUserId_ClientMutationId", table: "Messages", columns: new[] { "SupportRequestId", "AuthorUserId", "ClientMutationId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Messages_AuthorUserId", table: "Messages", column: "AuthorUserId");
        migrationBuilder.CreateIndex(name: "IX_CommandReceipts_ActorUserId_IdempotencyKey", table: "CommandReceipts", columns: new[] { "ActorUserId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_CommandReceipts_CreatedAt", table: "CommandReceipts", column: "CreatedAt");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Users_StatusDeactivatedAt",
            table: "Users",
            sql: "([Status] = N'Active' AND [DeactivatedAt] IS NULL) OR ([Status] = N'Deactivated' AND [DeactivatedAt] IS NOT NULL)");
        migrationBuilder.AddCheckConstraint(
            name: "CK_Teams_StatusDeactivatedAt",
            table: "Teams",
            sql: "([Status] = N'Active' AND [DeactivatedAt] IS NULL) OR ([Status] = N'Deactivated' AND [DeactivatedAt] IS NOT NULL)");
        migrationBuilder.AddCheckConstraint(
            name: "CK_RoleAssignments_RoleScope",
            table: "RoleAssignments",
            sql: "([Role] IN (N'GlobalAdministrator', N'GlobalSupportUser') AND [TeamId] IS NULL) OR ([Role] IN (N'TeamAdministrator', N'TeamUser') AND [TeamId] IS NOT NULL)");
        migrationBuilder.AddCheckConstraint(
            name: "CK_RoleAssignments_Revocation",
            table: "RoleAssignments",
            sql: "([RevokedAt] IS NULL AND [RevokedByUserId] IS NULL AND [RevocationReason] IS NULL) OR ([RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL AND NULLIF(LTRIM(RTRIM([RevocationReason])), N'') IS NOT NULL)");
        migrationBuilder.AddCheckConstraint(
            name: "CK_Invitations_State",
            table: "Invitations",
            sql: "([Status] = N'Accepted' AND [AcceptedAt] IS NOT NULL) OR ([Status] <> N'Accepted' AND [AcceptedAt] IS NULL)");

        migrationBuilder.Sql("CREATE TRIGGER [TR_Messages_Immutable] ON [Messages] AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 51000, 'Messages are immutable.', 1; END;");
        migrationBuilder.Sql("CREATE TRIGGER [TR_AuditEvents_AppendOnly] ON [AuditEvents] AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 51001, 'Audit events are append-only.', 1; END;");
        migrationBuilder.Sql("CREATE TRIGGER [TR_CommandReceipts_AppendOnly] ON [CommandReceipts] AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 51002, 'Command receipts are append-only.', 1; END;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_CommandReceipts_AppendOnly]; DROP TRIGGER IF EXISTS [TR_AuditEvents_AppendOnly]; DROP TRIGGER IF EXISTS [TR_Messages_Immutable];");
        migrationBuilder.DropTable(name: "CommandReceipts");
        migrationBuilder.DropTable(name: "Messages");
        migrationBuilder.DropTable(name: "AuditEvents");
        migrationBuilder.DropTable(name: "Invitations");
        migrationBuilder.DropTable(name: "SupportRequests");
        migrationBuilder.DropTable(name: "RoleAssignments");
        migrationBuilder.DropTable(name: "Teams");
        migrationBuilder.DropTable(name: "Users");
    }
}