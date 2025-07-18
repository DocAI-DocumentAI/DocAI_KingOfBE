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
    public class AIRequestLogConfig : IEntityTypeConfiguration<AIRequestLog>
    {
        public void Configure(EntityTypeBuilder<AIRequestLog> builder)
        {
            builder.ToTable("AIRequestLogs");

            builder.HasKey(e => e.Id);

            builder.HasIndex(e => e.RequestId)
                .IsUnique()
                .HasDatabaseName("IX_AIRequestLog_RequestId");

            builder.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_AIRequestLog_UserId");

            builder.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_AIRequestLog_CreatedAt");

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

            builder.Property(e => e.RequestContent)
                .HasColumnType("jsonb");

            builder.Property(e => e.ResponseContent)
                .HasColumnType("jsonb");

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}