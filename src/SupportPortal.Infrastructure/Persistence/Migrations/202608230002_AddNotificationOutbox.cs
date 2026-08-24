using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportPortal.Infrastructure.Persistence.Migrations;

public partial class AddNotificationOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SupportRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssigneeUserIdAtEvent = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                EventOccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                RecipientCount = table.Column<int>(type: "int", nullable: false),
                RecipientsExpandedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                RowVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                table.ForeignKey("FK_Notifications_Users_ActorUserId", x => x.ActorUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Notifications_Users_AssigneeUserIdAtEvent", x => x.AssigneeUserIdAtEvent, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Notifications_SupportRequests_SupportRequestId", x => x.SupportRequestId, "SupportRequests", "SupportRequestId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Notifications_Invitations_InvitationId", x => x.InvitationId, "Invitations", "InvitationId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "NotificationDeliveries",
            columns: table => new
            {
                NotificationDeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecipientKind = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RecipientAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                RecipientKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                State = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LeaseOwner = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastHttpStatus = table.Column<int>(type: "int", nullable: true),
                LastFailureCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ProviderMessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                PermanentFailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                SuppressedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationDeliveries", x => x.NotificationDeliveryId);
                table.ForeignKey("FK_NotificationDeliveries_Notifications_NotificationId", x => x.NotificationId, "Notifications", "NotificationId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_NotificationDeliveries_Users_RecipientUserId", x => x.RecipientUserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "NotificationAttempts",
            columns: table => new
            {
                NotificationAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                NotificationDeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttemptNumber = table.Column<int>(type: "int", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Outcome = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                HttpStatus = table.Column<int>(type: "int", nullable: true),
                FailureCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                RetryNotBefore = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ProviderMessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationAttempts", x => x.NotificationAttemptId);
                table.ForeignKey("FK_NotificationAttempts_NotificationDeliveries_NotificationDeliveryId", x => x.NotificationDeliveryId, "NotificationDeliveries", "NotificationDeliveryId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_EventType_SourceEntityId",
            table: "Notifications",
            columns: new[] { "EventType", "SourceEntityId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_Status_CreatedAt",
            table: "Notifications",
            columns: new[] { "Status", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_ActorUserId",
            table: "Notifications",
            column: "ActorUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_AssigneeUserIdAtEvent",
            table: "Notifications",
            column: "AssigneeUserIdAtEvent");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_SupportRequestId",
            table: "Notifications",
            column: "SupportRequestId");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_InvitationId",
            table: "Notifications",
            column: "InvitationId");

        migrationBuilder.CreateIndex(
            name: "IX_NotificationDeliveries_NotificationId_RecipientKey",
            table: "NotificationDeliveries",
            columns: new[] { "NotificationId", "RecipientKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_NotificationDeliveries_State_NextAttemptAt_LeaseExpiresAt",
            table: "NotificationDeliveries",
            columns: new[] { "State", "NextAttemptAt", "LeaseExpiresAt" });

        migrationBuilder.CreateIndex(
            name: "IX_NotificationDeliveries_RecipientUserId",
            table: "NotificationDeliveries",
            column: "RecipientUserId");

        migrationBuilder.CreateIndex(
            name: "IX_NotificationAttempts_NotificationDeliveryId_AttemptNumber",
            table: "NotificationAttempts",
            columns: new[] { "NotificationDeliveryId", "AttemptNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_NotificationAttempts_Outcome_CompletedAt",
            table: "NotificationAttempts",
            columns: new[] { "Outcome", "CompletedAt" });

        migrationBuilder.CreateCheckConstraint(
            name: "CK_Notifications_SourceContext",
            table: "Notifications",
            sql: "(([EventType] = N'InvitationCreated' AND [InvitationId] IS NOT NULL AND [SupportRequestId] IS NULL) OR ([EventType] <> N'InvitationCreated' AND [InvitationId] IS NULL AND [SupportRequestId] IS NOT NULL))");

        migrationBuilder.CreateCheckConstraint(
            name: "CK_NotificationDeliveries_RecipientDetails",
            table: "NotificationDeliveries",
            sql: "(([RecipientKind] = N'PortalUser' AND [RecipientUserId] IS NOT NULL AND [RecipientAddress] IS NULL) OR ([RecipientKind] = N'ConfiguredGlobalMailbox' AND [RecipientUserId] IS NULL AND [RecipientAddress] IS NOT NULL) OR ([RecipientKind] = N'InvitationRecipient' AND [RecipientUserId] IS NULL AND [RecipientAddress] IS NULL))");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "NotificationAttempts");
        migrationBuilder.DropTable(name: "NotificationDeliveries");
        migrationBuilder.DropTable(name: "Notifications");
    }
}