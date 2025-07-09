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
                }
            );
        }
    }
}
