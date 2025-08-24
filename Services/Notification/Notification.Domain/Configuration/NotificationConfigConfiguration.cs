using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Models;
using Notification.Domain.Enums;
using Shared.Utils;

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

            // ✅ Configure DateTime properties to use UTC (PostgreSQL safe)
            builder.Property(e => e.CreateAt)
                .HasColumnType("timestamp with time zone");
            
            builder.Property(e => e.UpdateAt)
                .HasColumnType("timestamp with time zone");

            // ✅ Seed the default configuration with FIXED cron expressions
            builder.HasData(
                new NotificationConfig
                {
                    Id = Guid.Parse("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                    ConfigKey = "Default",
                    WarningThresholdDays = 7,
                    // ✅ FIX: Correct cron expressions
                    ExpiredNotificationCron = "0 0 6 * * ?",        // 6:00 AM daily (FIXED from "0 0 8 * *?")
                    NearExpiredNotificationCron = "0 0 6 * * ?",     // 6:00 AM daily (FIXED from "0 0 9 ? * MON")
                    EnableExpiredNotifications = true,
                    EnableNearExpiredNotifications = true,
                    NearExpiredMode = NotificationMode.Daily,        // ✅ Changed to Daily to match 6 AM daily schedule
                    QuartzEnabled = true,
                    LogRetentionDays = 90,
                    // ✅ Use TimeZoneHelper to ensure UTC DateTimeKind for PostgreSQL
                    CreateAt = TimeZoneHelper.CreateUtcDateTime(2024, 1, 1, 0, 0, 0),
                    UpdateAt = TimeZoneHelper.CreateUtcDateTime(2024, 1, 1, 0, 0, 0)
                }
            );
        }
    }
}
