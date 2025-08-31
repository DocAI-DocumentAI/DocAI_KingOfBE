using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "AssociatedEvent", "BodyHtml", "CreateAt", "CreatedBy", "IsDeleted", "LastUpdatedBy", "Subject", "TemplateName", "UpdateAt" },
                values: new object[] { new Guid("c3c4c5c6-d7d8-e9e0-f1f2-333333333333"), "Expired", "\r\n        <div style='background-color: #fff3cd; border: 1px solid #ffeaa7; border-radius: 8px; padding: 20px; margin: 10px 0;'>\r\n            <h3 style='color: #856404; margin-top: 0;'>⚠️ Document Expiration Alert</h3>\r\n            <p>Dear {{UserName}},</p>\r\n            <p><strong style='color: #d63031;'>{{DocumentCount}} documents</strong> in <strong>{{DepartmentName}}</strong> department have <strong style='color: #d63031;'>EXPIRED</strong> as of {{TimeRange}}:</p>\r\n        </div>\r\n        \r\n        {{DocumentsList}}\r\n        \r\n        <div style='background-color: #f8f9fa; border-left: 4px solid #d63031; padding: 15px; margin: 20px 0;'>\r\n            <h4 style='color: #d63031; margin-top: 0;'>📋 Important Notice</h4>\r\n            <p><strong>These documents have been automatically moved to 'Archived' status</strong> and are no longer accessible to users.</p>\r\n            <p>If any of these documents should remain active, please contact your system administrator immediately to review and restore them.</p>\r\n        </div>\r\n        \r\n        <div style='background-color: #e7f3ff; border: 1px solid #b3d7ff; border-radius: 8px; padding: 15px; margin: 20px 0;'>\r\n            <h4 style='color: #0066cc; margin-top: 0;'>💡 Recommended Actions</h4>\r\n            <ul>\r\n                <li>Review expired documents to determine if new versions are needed</li>\r\n                <li>Update or create replacement documents where necessary</li>\r\n                <li>Notify affected team members about document status changes</li>\r\n                <li>Check if any processes or procedures need to be updated</li>\r\n            </ul>\r\n        </div>\r\n        \r\n        <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'>\r\n        <p style='font-size: 12px; color: #6c757d;'>\r\n            Generated at {{VietnamTime}} (Vietnam Time) - DocAI System<br>\r\n            This is an automated notification. Please do not reply to this email.\r\n        </p>", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "", false, null, "[{{DepartmentName}}] URGENT - {{DocumentCount}} documents have EXPIRED", "DailyExpiredDocuments", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("c3c4c5c6-d7d8-e9e0-f1f2-333333333333"));
        }
    }
}
