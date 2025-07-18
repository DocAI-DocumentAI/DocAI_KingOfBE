using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AI.Domain.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace AI.Domain.Configuration
{
    public class UsageMetricConfig : IEntityTypeConfiguration<UsageMetric>
    {
        public void Configure(EntityTypeBuilder<UsageMetric> builder)
        {
            builder.ToTable("UsageMetrics");

            builder.HasKey(e => e.Id);

            builder.HasIndex(e => e.RequestId)
                .HasDatabaseName("IX_UsageMetric_RequestId");

            builder.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_UsageMetric_UserId");

            builder.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_UsageMetric_CreatedAt");

            builder.HasIndex(e => new { e.ModelType, e.CreatedAt })
                .HasDatabaseName("IX_UsageMetric_Type_Date");

            builder.Property(e => e.RequestId)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.ModelType)
                .HasConversion<string>()
                .IsRequired();

            builder.Property(e => e.Status)
                .HasConversion<string>()
                .IsRequired();

            builder.Property(e => e.ErrorMessage)
                .HasMaxLength(1000);

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
