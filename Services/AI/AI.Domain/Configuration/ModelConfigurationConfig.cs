using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using AI.Domain.Models;

namespace AI.Domain.Configuration
{
    public class ModelConfigurationConfig : IEntityTypeConfiguration<ModelConfiguration>
    {
        public void Configure(EntityTypeBuilder<ModelConfiguration> builder)
        {
            builder.ToTable("ModelConfigurations");

            builder.HasKey(e => e.Id);

            builder.HasIndex(e => new { e.ModelType, e.IsActive })
                .HasDatabaseName("IX_ModelConfiguration_Type_Active");

            builder.Property(e => e.ModelType)
                .HasConversion<string>()
                .IsRequired();

            builder.Property(e => e.ModelName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Endpoint)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(e => e.MaxTokens)
                .HasDefaultValue(2048);

            builder.Property(e => e.Temperature)
                .HasDefaultValue(0.7)
                .HasPrecision(3, 2);

            builder.Property(e => e.TopP)
                .HasDefaultValue(0.9)
                .HasPrecision(3, 2);

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
