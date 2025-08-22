using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Notification.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ScanCronExpression",
                table: "NotificationConfigs",
                newName: "NearExpiredNotificationCron");

            migrationBuilder.AddColumn<bool>(
                name: "EnableExpiredNotifications",
                table: "NotificationConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableNearExpiredNotifications",
                table: "NotificationConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExpiredNotificationCron",
                table: "NotificationConfigs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "NearExpiredMode",
                table: "NotificationConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-1234-567890abcdef"),
                column: "BodyHtml",
                value: "\r\n                        <p>Dear {{UserName}},</p>\r\n                        <p>This is a reminder that the document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) is scheduled to expire on <b>{{EffectiveUntil}}</b>.</p>\r\n                        <p>Please review and take necessary action: <a href='{{DocumentLink}}'>View Document</a>.</p>\r\n                        <hr>\r\n                        <p><small>If you have already taken action, you can <a href='{{DismissLink}}'>dismiss future notifications for this version</a>.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-f6a7-8901-2345-67890abcdef1"),
                column: "BodyHtml",
                value: "\r\n                        <p>Dear {{UserName}},</p>\r\n                        <p>The document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) expired on <b>{{EffectiveUntil}}</b> and is no longer active.</p>\r\n                        <p>The document's status has been automatically updated to 'Expired'. Please review: <a href='{{DocumentLink}}'>View Document</a>.</p>\r\n                        <hr>\r\n                        <p><small>You can <a href='{{DismissLink}}'>dismiss any further related notifications for this version</a>.</small></p>");

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "AssociatedEvent", "BodyHtml", "CreateAt", "CreatedBy", "IsDeleted", "LastUpdatedBy", "Subject", "TemplateName", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("a1a2a3a4-b5b6-c7c8-d9d0-111111111111"), "General", "\r\n        <p>Dear {{UserName}},</p>\r\n        <p>Weekly summary for <strong>{{DepartmentName}}</strong> department ({{WeekRange}}):</p>\r\n        <p><strong>{{DocumentCount}} documents</strong> are nearing expiration and need attention.</p>\r\n        {{DocumentsList}}\r\n        <p>Please review and take necessary actions before expiration dates.</p>\r\n        <hr>\r\n        <p><small>Generated at {{VietnamTime}} - DocAI System</small></p>", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", false, null, "[{{DepartmentName}}] Weekly Summary - {{DocumentCount}} documents expiring", "WeeklyDocumentExpiration", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b2b3b4b5-c6c7-d8d9-e0e1-222222222222"), "General", "\r\n        <p>Dear {{UserName}},</p>\r\n        <p>Daily alert for <strong>{{DepartmentName}}</strong> department ({{TimeRange}}):</p>\r\n        <p><strong>{{DocumentCount}} documents</strong> require immediate attention as they are expiring soon.</p>\r\n        {{DocumentsList}}\r\n        <p><strong>Action required:</strong> Please review these documents immediately.</p>\r\n        <hr>\r\n        <p><small>Generated at {{VietnamTime}} - DocAI System</small></p>", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", false, null, "[{{DepartmentName}}] Daily Alert - {{DocumentCount}} documents need attention", "DailyDocumentExpiration", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                table: "NotificationConfigs",
                keyColumn: "Id",
                keyValue: new Guid("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                columns: new[] { "EnableExpiredNotifications", "EnableNearExpiredNotifications", "ExpiredNotificationCron", "NearExpiredMode", "NearExpiredNotificationCron" },
                values: new object[] { true, true, "0 0 8 * * ?", 1, "0 0 9 ? * MON" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("a1a2a3a4-b5b6-c7c8-d9d0-111111111111"));

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("b2b3b4b5-c6c7-d8d9-e0e1-222222222222"));

            migrationBuilder.DropColumn(
                name: "EnableExpiredNotifications",
                table: "NotificationConfigs");

            migrationBuilder.DropColumn(
                name: "EnableNearExpiredNotifications",
                table: "NotificationConfigs");

            migrationBuilder.DropColumn(
                name: "ExpiredNotificationCron",
                table: "NotificationConfigs");

            migrationBuilder.DropColumn(
                name: "NearExpiredMode",
                table: "NotificationConfigs");

            migrationBuilder.RenameColumn(
                name: "NearExpiredNotificationCron",
                table: "NotificationConfigs",
                newName: "ScanCronExpression");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-1234-567890abcdef"),
                column: "BodyHtml",
                value: "\r\n            <p>Dear {{UserName}},</p>\r\n            <p>This is a reminder that the document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) is scheduled to expire on <b>{{EffectiveUntil}}</b>.</p>\r\n            <p>Please review and take necessary action: <a href='{{DocumentLink}}'>View Document</a>.</p>\r\n            <hr>\r\n            <p><small>If you have already taken action, you can <a href='{{DismissLink}}'>dismiss future notifications for this version</a>.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-f6a7-8901-2345-67890abcdef1"),
                column: "BodyHtml",
                value: "\r\n            <p>Dear {{UserName}},</p>\r\n            <p>The document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) expired on <b>{{EffectiveUntil}}</b> and is no longer active.</p>\r\n            <p>The document's status has been automatically updated to 'Expired'. Please review: <a href='{{DocumentLink}}'>View Document</a>.</p>\r\n            <hr>\r\n            <p><small>You can <a href='{{DismissLink}}'>dismiss any further related notifications for this version</a>.</small></p>");

            migrationBuilder.UpdateData(
                table: "NotificationConfigs",
                keyColumn: "Id",
                keyValue: new Guid("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                column: "ScanCronExpression",
                value: "0 0 7 * * ?");
        }
    }
}
