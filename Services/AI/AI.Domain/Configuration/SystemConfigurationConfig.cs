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
    public class SystemConfigurationConfig : IEntityTypeConfiguration<SystemConfiguration>
    {
        public void Configure(EntityTypeBuilder<SystemConfiguration> builder)
        {
            builder.ToTable("SystemConfigurations");

            builder.HasKey(e => e.Id);

            builder.HasIndex(e => e.Key)
                .IsUnique()
                .HasDatabaseName("IX_SystemConfiguration_Key");

            builder.Property(e => e.Key)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Value)
                .IsRequired();

            builder.Property(e => e.Category)
                .HasMaxLength(50)
                .HasDefaultValue("General");

            builder.Property(e => e.Description)
                .HasMaxLength(500);

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
