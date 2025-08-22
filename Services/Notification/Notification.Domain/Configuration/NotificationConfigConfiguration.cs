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
    public class NotificationConfigConfiguration : IEntityTypeConfiguration<NotificationConfig>
    {
        public void Configure(EntityTypeBuilder<NotificationConfig> builder)
        {
            builder.ToTable("NotificationConfigs");
            builder.HasKey(nc => nc.Id);
            builder.HasIndex(nc => nc.ConfigKey).IsUnique();

            // Configure enum conversion
            builder.Property(e => e.NearExpiredMode)
                .HasConversion<int>();

            // Seed the default configuration
            builder.HasData(
                new NotificationConfig
                {
                    Id = Guid.Parse("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                    ConfigKey = "Default",
                    WarningThresholdDays = 7,
                    ExpiredNotificationCron = "0 0 8 * * ?",
                    NearExpiredNotificationCron = "0 0 9 ? * MON",
                    EnableExpiredNotifications = true,
                    EnableNearExpiredNotifications = true,
                    NearExpiredMode = NotificationMode.Weekly,
                    QuartzEnabled = true,
                    LogRetentionDays = 90,
                    CreateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    UpdateAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}
