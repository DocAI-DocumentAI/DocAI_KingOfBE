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
                    Id = Guid.Parse("a1b2c3d4-e5f6-7890-1234-567890abcdef"),
                    TemplateName = "DocumentNearingExpiration",
                    Subject = "[DocAI Reminder] Document '{{DocumentTitle}}' is Nearing Expiration",
                    BodyHtml = "<p>Dear User,</p><p>This is a reminder that the document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) is scheduled to expire on <b>{{EffectiveUntil}}</b>.</p><p>Please review and take necessary action: <a href='{{DocumentLink}}'>View Document</a>.</p><hr><p><small>If you have already taken action, you can <a href='{{DismissLink}}'>dismiss future notifications for this version</a>.</small></p>",
                    AssociatedEvent = NotificationType.NearingExpiration.ToString(),
                    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new EmailTemplate
                {
                    Id = Guid.Parse("b2c3d4e5-f6a7-8901-2345-67890abcdef1"),
                    TemplateName = "DocumentExpired",
                    Subject = "[DocAI Alert] Document '{{DocumentTitle}}' Has Expired",
                    BodyHtml = "<p>Dear User,</p><p>The document <b>'{{DocumentTitle}}'</b> (version <b>{{DocumentVersion}}</b>) expired on <b>{{EffectiveUntil}}</b> and is no longer active.</p><p>The document's status has been automatically updated to 'Expired'. Please review: <a href='{{DocumentLink}}'>View Document</a>.</p><hr><p><small>You can <a href='{{DismissLink}}'>dismiss any further related notifications for this version</a>.</small></p>",
                    AssociatedEvent = NotificationType.Expired.ToString(),
                    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new EmailTemplate
                {
                    Id = Guid.Parse("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                    TemplateName = "DocumentSubmitted",
                    Subject = "[DocAI Workflow] Document '{{DocumentTitle}}' Submitted for Approval",
                    BodyHtml = "<p>Dear Manager,</p><p>A new document has been submitted for your review and approval:</p><ul><li><b>Document Title:</b> {{DocumentTitle}}</li><li><b>Version:</b> {{DocumentVersion}}</li><li><b>Submitted By:</b> {{SubmittedBy}}</li><li><b>Department:</b> {{DepartmentName}}</li><li><b>Submission Date:</b> {{SubmissionDate}}</li></ul><p>Please review the document and take appropriate action: <a href='{{DocumentLink}}'>Review Document</a></p><p>You can approve or reject this document through the approval queue in the DocAI system.</p><hr><p><small>This is an automated notification from the DocAI document management system.</small></p>",
                    AssociatedEvent = NotificationType.DocumentSubmitted.ToString(),
                    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new EmailTemplate
                {
                    Id = Guid.Parse("d4e5f6a7-b8c9-0123-4567-890abcdef123"),
                    TemplateName = "DocumentApproved",
                    Subject = "[DocAI Workflow] Document '{{DocumentTitle}}' Approved",
                    BodyHtml = "<p>Dear {{DocumentOwner}},</p><p>Great news! Your document has been approved:</p><ul><li><b>Document Title:</b> {{DocumentTitle}}</li><li><b>Version:</b> {{DocumentVersion}}</li><li><b>Approved By:</b> {{ApprovedBy}}</li><li><b>Approval Date:</b> {{ApprovalDate}}</li><li><b>Comments:</b> {{Comments}}</li></ul><p>Your document is now available to authorized users. You can view it here: <a href='{{DocumentLink}}'>View Document</a></p><hr><p><small>This is an automated notification from the DocAI document management system.</small></p>",
                    AssociatedEvent = NotificationType.DocumentApproved.ToString(),
                    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new EmailTemplate
                {
                    Id = Guid.Parse("e5f6a7b8-c9d0-1234-5678-90abcdef1234"),
                    TemplateName = "DocumentRejected",
                    Subject = "[DocAI Workflow] Document '{{DocumentTitle}}' Requires Revision",
                    BodyHtml = "<p>Dear {{DocumentOwner}},</p><p>Your document submission requires revision before approval:</p><ul><li><b>Document Title:</b> {{DocumentTitle}}</li><li><b>Version:</b> {{DocumentVersion}}</li><li><b>Reviewed By:</b> {{ReviewedBy}}</li><li><b>Review Date:</b> {{ReviewDate}}</li><li><b>Reason for Revision:</b> {{Comments}}</li></ul><p>Please review the feedback, make necessary changes, and resubmit your document: <a href='{{DocumentLink}}'>Edit Document</a></p><p>If you have questions about the feedback, please contact the reviewer or your department manager.</p><hr><p><small>This is an automated notification from the DocAI document management system.</small></p>",
                    AssociatedEvent = NotificationType.DocumentRejected.ToString(),
                    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new EmailTemplate
                {
                    Id = Guid.Parse("f6a7b8c9-d0e1-2345-6789-0abcdef12345"),
                    TemplateName = "DocumentPublished",
                    Subject = "[DocAI Update] New Document '{{DocumentTitle}}' Published",
                    BodyHtml = "<p>Dear {{UserName}},</p><p>A new document has been published and is now available for your department:</p><ul><li><b>Document Title:</b> {{DocumentTitle}}</li><li><b>Version:</b> {{DocumentVersion}}</li><li><b>Published By:</b> {{ApprovedBy}}</li><li><b>Publication Date:</b> {{ApprovalDate}}</li><li><b>Department:</b> {{DepartmentName}}</li><li><b>Document Type:</b> {{DocumentType}}</li></ul><p>You can access the document here: <a href='{{DocumentLink}}'>View Document</a></p><p>This document is now part of your department's official documentation and may contain important information relevant to your work.</p><hr><p><small>This is an automated notification from the DocAI document management system.</small></p>",
                    AssociatedEvent = NotificationType.DocumentUpdate.ToString(),
                    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}
