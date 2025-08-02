using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Notification.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    AssociatedEvent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WarningThresholdDays = table.Column<int>(type: "integer", nullable: false),
                    ScanCronExpression = table.Column<string>(type: "text", nullable: false),
                    QuartzEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LogRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersion = table.Column<string>(type: "text", nullable: true),
                    NotificationType = table.Column<int>(type: "integer", nullable: false),
                    RecipientType = table.Column<int>(type: "integer", nullable: false),
                    RecipientAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    IsSent = table.Column<bool>(type: "boolean", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    IsDismissed = table.Column<bool>(type: "boolean", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DismissedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DismissToken = table.Column<Guid>(type: "uuid", nullable: true),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "AssociatedEvent", "BodyHtml", "CreateAt", "Subject", "TemplateName", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-e5f6-7890-1234-567890abcdef"), "NearingExpiration", "<p>Dear User,</p><p>This is a reminder that the document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) is scheduled to expire on <b>{{EffectiveUntil}}</b>.</p><p>Please review and take necessary action: <a href='{{DocumentLink}}'>View Document</a>.</p><hr><p><small>If you have already taken action, you can <a href='{{DismissLink}}'>dismiss future notifications for this version</a>.</small></p>", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "[DocAI Reminder] Document '{{DocumentTitle}}' is Nearing Expiration", "DocumentNearingExpiration", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b2c3d4e5-f6a7-8901-2345-67890abcdef1"), "Expired", "<p>Dear User,</p><p>The document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) expired on <b>{{EffectiveUntil}}</b> and is no longer active.</p><p>The document's status has been automatically updated to 'Expired'. Please review: <a href='{{DocumentLink}}'>View Document</a>.</p><hr><p><small>You can <a href='{{DismissLink}}'>dismiss any further related notifications for this version</a>.</small></p>", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "[DocAI Alert] Document '{{DocumentTitle}}' Has Expired", "DocumentExpired", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "NotificationConfigs",
                columns: new[] { "Id", "ConfigKey", "CreateAt", "LogRetentionDays", "QuartzEnabled", "ScanCronExpression", "UpdateAt", "WarningThresholdDays" },
                values: new object[] { new Guid("c3d4e5f6-a7b8-9012-3456-7890abcdef12"), "Default", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 90, true, "0 0 7 * * ?", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 7 });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_TemplateName",
                table: "EmailTemplates",
                column: "TemplateName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationConfigs_ConfigKey",
                table: "NotificationConfigs",
                column: "ConfigKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_CreateAt",
                table: "NotificationLogs",
                column: "CreateAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_DismissToken",
                table: "NotificationLogs",
                column: "DismissToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_DocumentId",
                table: "NotificationLogs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_IsSent",
                table: "NotificationLogs",
                column: "IsSent");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_NotificationType",
                table: "NotificationLogs",
                column: "NotificationType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropTable(
                name: "NotificationConfigs");

            migrationBuilder.DropTable(
                name: "NotificationLogs");
        }
    }
}
