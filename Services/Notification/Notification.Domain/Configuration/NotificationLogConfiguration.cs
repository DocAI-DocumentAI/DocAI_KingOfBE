using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Models;
namespace Notification.Domain.Configuration;

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");

        // Primary key
        builder.HasKey(nl => nl.Id);

        // Required fields
        builder.Property(nl => nl.DocumentId).IsRequired();
        builder.Property(nl => nl.Subject).IsRequired();
        builder.Property(nl => nl.Message).IsRequired();
        builder.Property(nl => nl.NotificationType).IsRequired();
        builder.Property(nl => nl.RecipientType).IsRequired();

        // String length constraints
        builder.Property(nl => nl.RecipientAddress).HasMaxLength(255);

        // Indexes for performance
        builder.HasIndex(nl => nl.DocumentId);
        builder.HasIndex(nl => nl.DocumentVersion);
        builder.HasIndex(nl => nl.NotificationType);
        builder.HasIndex(nl => nl.IsSent);
        builder.HasIndex(nl => nl.SentAt);
        builder.HasIndex(nl => nl.CreateAt);

        builder.HasIndex(nl => new { nl.DocumentId, nl.DocumentVersion, nl.NotificationType, nl.RecipientAddress });
        builder.HasIndex(nl => new { nl.DocumentId, nl.DocumentVersion, nl.NotificationType, nl.SentAt });

    }

}