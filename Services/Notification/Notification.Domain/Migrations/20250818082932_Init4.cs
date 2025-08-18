using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                column: "BodyHtml",
                value: "\r\n            <p>Dear User,</p>\r\n            <p>A new document has been submitted for your review and approval:</p>\r\n            <ul>\r\n                <li><b>Document Title:</b> {{DocumentTitle}}</li>\r\n                <li><b>Version:</b> {{DocumentVersion}}</li>\r\n                <li><b>Submitted By:</b> {{SubmittedBy}}</li>\r\n                <li><b>Department:</b> {{DepartmentName}}</li>\r\n                <li><b>Submission Date:</b> {{SubmissionDate}}</li>\r\n            </ul>\r\n            <p>Please review the document and take appropriate action: <a href='{{DocumentLink}}'>Review Document</a></p>\r\n            <p>You can approve or reject this document through the approval queue in the DocAI system.</p>\r\n            <hr>\r\n            <p><small>This is an automated notification from the DocAI document management system.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("d4e5f6a7-b8c9-0123-4567-890abcdef123"),
                column: "BodyHtml",
                value: "\r\n            <p>Dear User,</p>\r\n            <p>Great news! Your document has been approved:</p>\r\n            <ul>\r\n                <li><b>Document Title:</b> {{DocumentTitle}}</li>\r\n                <li><b>Version:</b> {{DocumentVersion}}</li>\r\n                <li><b>Approved By:</b> {{SubmitterName}}</li>\r\n                <li><b>Approval Date:</b> {{SubmissionDate}}</li>\r\n                <li><b>Comments:</b> {{Comments}}</li>\r\n            </ul>\r\n            <p>Your document is now available to authorized users. You can view it here: <a href='{{DocumentLink}}'>View Document</a></p>\r\n            <hr>\r\n            <p><small>This is an automated notification from the DocAI document management system.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("e5f6a7b8-c9d0-1234-5678-90abcdef1234"),
                column: "BodyHtml",
                value: "\r\n            <p>Dear User,</p>\r\n            <p>Your document submission requires revision before approval:</p>\r\n            <ul>\r\n                <li><b>Document Title:</b> {{DocumentTitle}}</li>\r\n                <li><b>Version:</b> {{DocumentVersion}}</li>\r\n                <li><b>Reviewed By:</b> {{SubmitterName}}</li>\r\n                <li><b>Review Date:</b> {{SubmissionDate}}</li>\r\n                <li><b>Reason for Revision:</b> {{Comments}}</li>\r\n            </ul>\r\n            <p>Please review the feedback, make necessary changes, and resubmit your document: <a href='{{DocumentLink}}'>Edit Document</a></p>\r\n            <p>If you have questions about the feedback, please contact the reviewer or your department manager.</p>\r\n            <hr>\r\n            <p><small>This is an automated notification from the DocAI document management system.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("f6a7b8c9-d0e1-2345-6789-0abcdef12345"),
                column: "BodyHtml",
                value: "\r\n            <p>Dear User,</p>\r\n            <p>A new document has been published and is now available for your department:</p>\r\n            <ul>\r\n                <li><b>Document Title:</b> {{DocumentTitle}}</li>\r\n                <li><b>Version:</b> {{DocumentVersion}}</li>\r\n                <li><b>Published By:</b> {{SubmitterName}}</li>\r\n                <li><b>Publication Date:</b> {{SubmissionDate}}</li>\r\n                <li><b>Department:</b> {{DepartmentName}}</li>\r\n            </ul>\r\n            <p>You can access the document here: <a href='{{DocumentLink}}'>View Document</a></p>\r\n            <p>This document is now part of your department's official documentation and may contain important information relevant to your work.</p>\r\n            <hr>\r\n            <p><small>This is an automated notification from the DocAI document management system.</small></p>");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-7890-1234-567890abcdef"),
                column: "BodyHtml",
                value: "<p>Dear User,</p><p>This is a reminder that the document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) is scheduled to expire on <b>{{EffectiveUntil}}</b>.</p><p>Please review and take necessary action: <a href='{{DocumentLink}}'>View Document</a>.</p><hr><p><small>If you have already taken action, you can <a href='{{DismissLink}}'>dismiss future notifications for this version</a>.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-f6a7-8901-2345-67890abcdef1"),
                column: "BodyHtml",
                value: "<p>Dear User,</p><p>The document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) expired on <b>{{EffectiveUntil}}</b> and is no longer active.</p><p>The document's status has been automatically updated to 'Expired'. Please review: <a href='{{DocumentLink}}'>View Document</a>.</p><hr><p><small>You can <a href='{{DismissLink}}'>dismiss any further related notifications for this version</a>.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                column: "BodyHtml",
                value: "<p>Dear Manager,</p><p>A new document has been submitted for your review and approval:</p><ul><li><b>Document Title:</b> {{DocumentTitle}}</li><li><b>Version:</b> {{DocumentVersion}}</li><li><b>Submitted By:</b> {{SubmittedBy}}</li><li><b>Department:</b> {{DepartmentName}}</li><li><b>Submission Date:</b> {{SubmissionDate}}</li></ul><p>Please review the document and take appropriate action: <a href='{{DocumentLink}}'>Review Document</a></p><p>You can approve or reject this document through the approval queue in the DocAI system.</p><hr><p><small>This is an automated notification from the DocAI document management system.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("d4e5f6a7-b8c9-0123-4567-890abcdef123"),
                column: "BodyHtml",
                value: "<p>Dear {{DocumentOwner}},</p><p>Great news! Your document has been approved:</p><ul><li><b>Document Title:</b> {{DocumentTitle}}</li><li><b>Version:</b> {{DocumentVersion}}</li><li><b>Approved By:</b> {{ApprovedBy}}</li><li><b>Approval Date:</b> {{ApprovalDate}}</li><li><b>Comments:</b> {{Comments}}</li></ul><p>Your document is now available to authorized users. You can view it here: <a href='{{DocumentLink}}'>View Document</a></p><hr><p><small>This is an automated notification from the DocAI document management system.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("e5f6a7b8-c9d0-1234-5678-90abcdef1234"),
                column: "BodyHtml",
                value: "<p>Dear {{DocumentOwner}},</p><p>Your document submission requires revision before approval:</p><ul><li><b>Document Title:</b> {{DocumentTitle}}</li><li><b>Version:</b> {{DocumentVersion}}</li><li><b>Reviewed By:</b> {{ReviewedBy}}</li><li><b>Review Date:</b> {{ReviewDate}}</li><li><b>Reason for Revision:</b> {{Comments}}</li></ul><p>Please review the feedback, make necessary changes, and resubmit your document: <a href='{{DocumentLink}}'>Edit Document</a></p><p>If you have questions about the feedback, please contact the reviewer or your department manager.</p><hr><p><small>This is an automated notification from the DocAI document management system.</small></p>");

            migrationBuilder.UpdateData(
                table: "EmailTemplates",
                keyColumn: "Id",
                keyValue: new Guid("f6a7b8c9-d0e1-2345-6789-0abcdef12345"),
                column: "BodyHtml",
                value: "<p>Dear {{UserName}},</p><p>A new document has been published and is now available for your department:</p><ul><li><b>Document Title:</b> {{DocumentTitle}}</li><li><b>Version:</b> {{DocumentVersion}}</li><li><b>Published By:</b> {{ApprovedBy}}</li><li><b>Publication Date:</b> {{ApprovalDate}}</li><li><b>Department:</b> {{DepartmentName}}</li><li><b>Document Type:</b> {{DocumentType}}</li></ul><p>You can access the document here: <a href='{{DocumentLink}}'>View Document</a></p><p>This document is now part of your department's official documentation and may contain important information relevant to your work.</p><hr><p><small>This is an automated notification from the DocAI document management system.</small></p>");
        }
    }
}
