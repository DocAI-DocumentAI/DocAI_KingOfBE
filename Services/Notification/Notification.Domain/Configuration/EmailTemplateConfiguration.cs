using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Models;
using Notification.Domain.Enums;

namespace Notification.Domain.Configuration
{
    public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
    {
        public void Configure(EntityTypeBuilder<EmailTemplate> builder)
        {
            builder.ToTable("EmailTemplates");
            builder.HasKey(et => et.Id);

            // Ensure template names are unique for easy retrieval
            builder.HasIndex(et => et.TemplateName).IsUnique();

            builder.Property(et => et.TemplateName).IsRequired().HasMaxLength(100);
            builder.Property(et => et.Subject).IsRequired().HasMaxLength(255);
            builder.Property(et => et.BodyHtml).IsRequired();


            builder.HasData(
new EmailTemplate
{
    Id = Guid.Parse("b2c3d4e5-f6a7-8901-2345-67890abcdef1"),
    TemplateName = "DocumentExpired",
    Subject = "[DocAI Alert] Document '{{DocumentTitle}}' Has Expired",
    BodyHtml = @"
        <p>Dear {{UserName}},</p>
        <p>The document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) expired on <b>{{EffectiveUntil}}</b> and is no longer active.</p>
        <p>The document's status has been automatically updated to 'Expired'.</p>
        <p><strong>Note:</strong> This document is no longer accessible as it has passed its expiration date.</p>
        <hr>
        <p><small>This is an automated notification from the DocAI document management system.</small></p>",
    AssociatedEvent = NotificationType.Expired.ToString(),
    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
},

new EmailTemplate
{
    Id = Guid.Parse("a1b2c3d4-e5f6-7890-1234-567890abcdef"),
    TemplateName = "DocumentNearingExpiration",
    Subject = "[DocAI Reminder] Document '{{DocumentTitle}}' is Nearing Expiration",
    BodyHtml = @"
        <p>Dear {{UserName}},</p>
        <p>This is a reminder that the document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) is scheduled to expire on <b>{{EffectiveUntil}}</b>.</p>
        <p>Please review and take necessary action: <a href='{{DocumentLink}}'>View Document</a>.</p>
        <hr>
        <p><small>This is an automated notification from the DocAI document management system.</small></p>",
    AssociatedEvent = NotificationType.NearingExpiration.ToString(),
    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
},
      new EmailTemplate
      {
          Id = Guid.Parse("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
          TemplateName = "DocumentSubmitted",
          Subject = "[DocAI Workflow] Document '{{DocumentTitle}}' Submitted for Approval",
          BodyHtml = @"
            <p>Dear User,</p>
            <p>A new document has been submitted for your review and approval:</p>
            <ul>
                <li><b>Document Title:</b> {{DocumentTitle}}</li>
                <li><b>Version:</b> {{DocumentVersion}}</li>
                <li><b>Submitted By:</b> {{SubmittedBy}}</li>
                <li><b>Department:</b> {{DepartmentName}}</li>
                <li><b>Submission Date:</b> {{SubmissionDate}}</li>
            </ul>
            <p>Please review the document and take appropriate action: <a href='{{DocumentLink}}'>Review Document</a></p>
            <p>You can approve or reject this document through the approval queue in the DocAI system.</p>
            <hr>
            <p><small>This is an automated notification from the DocAI document management system.</small></p>",
          AssociatedEvent = NotificationType.DocumentSubmitted.ToString(),
          CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
          UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
      },

    // ✅ FIXED: DocumentApproved - Changed "{{DocumentOwner}}" to "{{UserName}}"
    new EmailTemplate
    {
        Id = Guid.Parse("d4e5f6a7-b8c9-0123-4567-890abcdef123"),
        TemplateName = "DocumentApproved",
        Subject = "[DocAI Workflow] Document '{{DocumentTitle}}' Approved",
        BodyHtml = @"
            <p>Dear User,</p>
            <p>Great news! Your document has been approved:</p>
            <ul>
                <li><b>Document Title:</b> {{DocumentTitle}}</li>
                <li><b>Version:</b> {{DocumentVersion}}</li>
                <li><b>Approved By:</b> {{SubmitterName}}</li>
                <li><b>Approval Date:</b> {{SubmissionDate}}</li>
                <li><b>Comments:</b> {{Comments}}</li>
            </ul>
            <p>Your document is now available to authorized users. You can view it here: <a href='{{DocumentLink}}'>View Document</a></p>
            <hr>
            <p><small>This is an automated notification from the DocAI document management system.</small></p>",
        AssociatedEvent = NotificationType.DocumentApproved.ToString(),
        CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    },

    // ✅ FIXED: DocumentRejected - Changed "{{DocumentOwner}}" to "{{UserName}}"
    new EmailTemplate
    {
        Id = Guid.Parse("e5f6a7b8-c9d0-1234-5678-90abcdef1234"),
        TemplateName = "DocumentRejected",
        Subject = "[DocAI Workflow] Document '{{DocumentTitle}}' Requires Revision",
        BodyHtml = @"
            <p>Dear User,</p>
            <p>Your document submission requires revision before approval:</p>
            <ul>
                <li><b>Document Title:</b> {{DocumentTitle}}</li>
                <li><b>Version:</b> {{DocumentVersion}}</li>
                <li><b>Reviewed By:</b> {{SubmitterName}}</li>
                <li><b>Review Date:</b> {{SubmissionDate}}</li>
                <li><b>Reason for Revision:</b> {{Comments}}</li>
            </ul>
            <p>Please review the feedback, make necessary changes, and resubmit your document: <a href='{{DocumentLink}}'>Edit Document</a></p>
            <p>If you have questions about the feedback, please contact the reviewer or your department manager.</p>
            <hr>
            <p><small>This is an automated notification from the DocAI document management system.</small></p>",
        AssociatedEvent = NotificationType.DocumentRejected.ToString(),
        CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    },

    // ✅ KEEP: DocumentPublished (already using {{UserName}})
    new EmailTemplate
    {
        Id = Guid.Parse("f6a7b8c9-d0e1-2345-6789-0abcdef12345"),
        TemplateName = "DocumentPublished",
        Subject = "[DocAI Update] New Document '{{DocumentTitle}}' Published",
        BodyHtml = @"
            <p>Dear User,</p>
            <p>A new document has been published and is now available for your department:</p>
            <ul>
                <li><b>Document Title:</b> {{DocumentTitle}}</li>
                <li><b>Version:</b> {{DocumentVersion}}</li>
                <li><b>Published By:</b> {{SubmitterName}}</li>
                <li><b>Publication Date:</b> {{SubmissionDate}}</li>
                <li><b>Department:</b> {{DepartmentName}}</li>
            </ul>
            <p>You can access the document here: <a href='{{DocumentLink}}'>View Document</a></p>
            <p>This document is now part of your department's official documentation and may contain important information relevant to your work.</p>
            <hr>
            <p><small>This is an automated notification from the DocAI document management system.</small></p>",
        AssociatedEvent = NotificationType.DocumentUpdate.ToString(),
        CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    },
    new EmailTemplate
    {
        Id = Guid.Parse("a1a2a3a4-b5b6-c7c8-d9d0-111111111111"),
        TemplateName = "WeeklyDocumentExpiration",
        Subject = "[{{DepartmentName}}] Weekly Summary - {{DocumentCount}} documents expiring",
        BodyHtml = @"
        <p>Dear {{UserName}},</p>
        <p>Weekly summary for <strong>{{DepartmentName}}</strong> department ({{WeekRange}}):</p>
        <p><strong>{{DocumentCount}} documents</strong> are nearing expiration and need attention.</p>
        {{DocumentsList}}
        <p>Please review and take necessary actions before expiration dates.</p>
        <hr>
        <p><small>Generated at {{VietnamTime}} - DocAI System</small></p>",
        AssociatedEvent = NotificationType.General.ToString(),
        CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    },

new EmailTemplate
{
    Id = Guid.Parse("b2b3b4b5-c6c7-d8d9-e0e1-222222222222"),
    TemplateName = "DailyDocumentExpiration",
    Subject = "[{{DepartmentName}}] Daily Alert - {{DocumentCount}} documents need attention",
    BodyHtml = @"
        <p>Dear {{UserName}},</p>
        <p>Daily alert for <strong>{{DepartmentName}}</strong> department ({{TimeRange}}):</p>
        <p><strong>{{DocumentCount}} documents</strong> require immediate attention as they are expiring soon.</p>
        {{DocumentsList}}
        <p><strong>Action required:</strong> Please review these documents immediately.</p>
        <hr>
        <p><small>Generated at {{VietnamTime}} - DocAI System</small></p>",
    AssociatedEvent = NotificationType.General.ToString(),
    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
},
new EmailTemplate
{
    Id = Guid.Parse("c3c4c5c6-d7d8-e9e0-f1f2-333333333333"),
    TemplateName = "DailyExpiredDocuments",
    Subject = "[{{DepartmentName}}] URGENT - {{DocumentCount}} documents have EXPIRED",
    BodyHtml = @"
        <div style='background-color: #fff3cd; border: 1px solid #ffeaa7; border-radius: 8px; padding: 20px; margin: 10px 0;'>
            <h3 style='color: #856404; margin-top: 0;'>⚠️ Document Expiration Alert</h3>
            <p>Dear {{UserName}},</p>
            <p><strong style='color: #d63031;'>{{DocumentCount}} documents</strong> in <strong>{{DepartmentName}}</strong> department have <strong style='color: #d63031;'>EXPIRED</strong> as of {{TimeRange}}:</p>
        </div>
        
        {{DocumentsList}}
        
        <div style='background-color: #f8f9fa; border-left: 4px solid #d63031; padding: 15px; margin: 20px 0;'>
            <h4 style='color: #d63031; margin-top: 0;'>📋 Important Notice</h4>
            <p><strong>These documents have been automatically moved to 'Archived' status</strong> and are no longer accessible to users.</p>
            <p>If any of these documents should remain active, please contact your system administrator immediately to review and restore them.</p>
        </div>
        
        <div style='background-color: #e7f3ff; border: 1px solid #b3d7ff; border-radius: 8px; padding: 15px; margin: 20px 0;'>
            <h4 style='color: #0066cc; margin-top: 0;'>💡 Recommended Actions</h4>
            <ul>
                <li>Review expired documents to determine if new versions are needed</li>
                <li>Update or create replacement documents where necessary</li>
                <li>Notify affected team members about document status changes</li>
                <li>Check if any processes or procedures need to be updated</li>
            </ul>
        </div>
        
        <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;'>
        <p style='font-size: 12px; color: #6c757d;'>
            Generated at {{VietnamTime}} (Vietnam Time) - DocAI System<br>
            This is an automated notification. Please do not reply to this email.
        </p>",
    AssociatedEvent = NotificationType.Expired.ToString(),
    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
}
            );
        }
    }
}
