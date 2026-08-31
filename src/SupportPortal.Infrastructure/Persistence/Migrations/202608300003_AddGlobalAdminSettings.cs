using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportPortal.Infrastructure.Persistence.Migrations;

public partial class AddGlobalAdminSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DeploymentSettings",
            columns: table => new
            {
                DeploymentSettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ScopeKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Revision = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProductName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ShortProductName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                LogoUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                FaviconUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                PrimaryColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                AccentColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                FocusColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                SupportContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                SupportContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                OrganizationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                InvitationAcceptanceBaseUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                InvitationLifetimeHours = table.Column<int>(type: "int", nullable: true),
                SendGridEnabled = table.Column<bool>(type: "bit", nullable: true),
                SendGridSenderDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                SendGridSenderAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                SendGridReplyToAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                SendGridPublicPortalUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                SendGridHttpTimeoutSeconds = table.Column<int>(type: "int", nullable: true),
                SendGridMaximumAttempts = table.Column<int>(type: "int", nullable: true),
                SendGridMinimumBackoffSeconds = table.Column<int>(type: "int", nullable: true),
                SendGridMaximumBackoffSeconds = table.Column<int>(type: "int", nullable: true),
                SendGridDataResidency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                SendGridBatchSize = table.Column<int>(type: "int", nullable: true),
                SendGridLeaseSeconds = table.Column<int>(type: "int", nullable: true),
                SendGridApiKeyMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                SendGridApiKeySecretVersion = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                RowVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeploymentSettings", x => x.DeploymentSettingsId);
                table.CheckConstraint("CK_DeploymentSettings_Scope", "[ScopeKey] = N'global'");
                table.CheckConstraint("CK_DeploymentSettings_ApiKeyReference", "([SendGridApiKeyMode] = N'Managed' AND [SendGridApiKeySecretVersion] IS NOT NULL) OR ([SendGridApiKeyMode] <> N'Managed' AND [SendGridApiKeySecretVersion] IS NULL)");
                table.ForeignKey(
                    name: "FK_DeploymentSettings_Users_UpdatedByUserId",
                    column: x => x.UpdatedByUserId,
                    principalTable: "Users",
                    principalColumn: "UserId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DeploymentSettingsRecipients",
            columns: table => new
            {
                DeploymentSettingsRecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DeploymentSettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NormalizedAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeploymentSettingsRecipients", x => x.DeploymentSettingsRecipientId);
                table.ForeignKey(
                    name: "FK_DeploymentSettingsRecipients_DeploymentSettings_DeploymentSettingsId",
                    column: x => x.DeploymentSettingsId,
                    principalTable: "DeploymentSettings",
                    principalColumn: "DeploymentSettingsId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DeploymentSettings_Revision",
            table: "DeploymentSettings",
            column: "Revision",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_DeploymentSettings_ScopeKey",
            table: "DeploymentSettings",
            column: "ScopeKey",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_DeploymentSettings_UpdatedByUserId",
            table: "DeploymentSettings",
            column: "UpdatedByUserId");
        migrationBuilder.CreateIndex(
            name: "IX_DeploymentSettingsRecipients_DeploymentSettingsId_NormalizedAddress",
            table: "DeploymentSettingsRecipients",
            columns: new[] { "DeploymentSettingsId", "NormalizedAddress" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DeploymentSettingsRecipients");
        migrationBuilder.DropTable(name: "DeploymentSettings");
    }
}
