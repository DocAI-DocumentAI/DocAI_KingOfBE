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
        builder.HasKey(nl => nl.Id);

        builder.Property(nl => nl.Subject).IsRequired();
        builder.Property(nl => nl.Message).IsRequired();

        builder.HasIndex(nl => nl.DocumentId);
        builder.HasIndex(nl => nl.NotificationType);
        builder.HasIndex(nl => nl.IsSent);
        builder.HasIndex(nl => nl.CreateAt);
        builder.HasIndex(nl => nl.DismissToken).IsUnique();

    }

}