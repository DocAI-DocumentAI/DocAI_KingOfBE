using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_DismissToken",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "DismissToken",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "DismissedAt",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "DismissedByUserId",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "IsDismissed",
                table: "NotificationLogs");

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "AssociatedEvent", "BodyHtml", "CreateAt", "CreatedBy", "IsDeleted", "LastUpdatedBy", "Subject", "TemplateName", "UpdateAt" },
                values: new object[] { new Guid("f6a7b8c9-d0e1-2345-6789-0abcdef12345"), "DocumentUpdate", "<p>Dear {{UserName}},</p><p>A new document has been published and is now available for your department:</p><ul><li><b>Document Title:</b> {{DocumentTitle}}</li><li><b>Version:</b> {{DocumentVersion}}</li><li><b>Published By:</b> {{ApprovedBy}}</li><li><b>Publication Date:</b> {{ApprovalDate}}</li><li><b>Department:</b> {{DepartmentName}}</li><li><b>Document Type:</b> {{DocumentType}}</li></ul><p>You can access the document here: <a href='{{DocumentLink}}'>View Document</a></p><p>This document is now part of your department's official documentation and may contain important information relevant to your work.</p><hr><p><small>This is an automated notification from the DocAI document management system.</small></p>", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", false, null, "[DocAI Update] New Document '{{DocumentTitle}}' Published", "DocumentPublished", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_DocumentId_DocumentVersion_NotificationTy~1",
                table: "NotificationLogs",
                columns: new[] { "DocumentId", "DocumentVersion", "NotificationType", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_DocumentId_DocumentVersion_NotificationTyp~",
                table: "NotificationLogs",
                columns: new[] { "DocumentId", "DocumentVersion", "NotificationType", "RecipientAddress" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_DocumentVersion",
                table: "NotificationLogs",
                column: "DocumentVersion");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_SentAt",
                table: "NotificationLogs",
                column: "SentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_DocumentId_DocumentVersion_NotificationTy~1",
                table: "NotificationLogs");

            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_DocumentId_DocumentVersion_NotificationTyp~",
                table: "NotificationLogs");

            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_DocumentVersion",
                table: "NotificationLogs");

            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_SentAt",
                table: "NotificationLogs");

            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("f6a7b8c9-d0e1-2345-6789-0abcdef12345"));

            migrationBuilder.AddColumn<Guid>(
                name: "DismissToken",
                table: "NotificationLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DismissedAt",
                table: "NotificationLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DismissedByUserId",
                table: "NotificationLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDismissed",
                table: "NotificationLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_DismissToken",
                table: "NotificationLogs",
                column: "DismissToken",
                unique: true);
        }
    }
}
