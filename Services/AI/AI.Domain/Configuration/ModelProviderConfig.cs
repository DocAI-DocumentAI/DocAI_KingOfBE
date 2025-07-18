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
    public class ModelProviderConfig : IEntityTypeConfiguration<ModelProvider>
    {
        public void Configure(EntityTypeBuilder<ModelProvider> builder)
        {
            builder.ToTable("ModelProviders");

            builder.HasKey(e => e.Id);

            builder.HasIndex(e => e.Name)
                .IsUnique()
                .HasDatabaseName("IX_ModelProvider_Name");

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.BaseUrl)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(e => e.ApiKeyName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Relationships
            builder.HasMany(e => e.ModelConfigurations)
                .WithOne()
                .HasForeignKey("ModelProviderId")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
