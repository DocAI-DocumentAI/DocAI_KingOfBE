using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-1234-567890abcdef"),
                column: "BodyHtml",
                value: "\r\n        <p>Dear {{UserName}},</p>\r\n        <p>This is a reminder that the document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) is scheduled to expire on <b>{{EffectiveUntil}}</b>.</p>\r\n        <p>Please review and take necessary action: <a href='{{DocumentLink}}'>View Document</a>.</p>\r\n        <hr>\r\n        <p><small>This is an automated notification from the DocAI document management system.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-f6a7-8901-2345-67890abcdef1"),
                column: "BodyHtml",
                value: "\r\n        <p>Dear {{UserName}},</p>\r\n        <p>The document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) expired on <b>{{EffectiveUntil}}</b> and is no longer active.</p>\r\n        <p>The document's status has been automatically updated to 'Expired'.</p>\r\n        <p><strong>Note:</strong> This document is no longer accessible as it has passed its expiration date.</p>\r\n        <hr>\r\n        <p><small>This is an automated notification from the DocAI document management system.</small></p>");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
