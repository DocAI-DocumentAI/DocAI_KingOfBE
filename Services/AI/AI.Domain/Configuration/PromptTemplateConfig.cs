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
    public class PromptTemplateConfig : IEntityTypeConfiguration<PromptTemplate>
    {
        public void Configure(EntityTypeBuilder<PromptTemplate> builder)
        {
            builder.ToTable("PromptTemplates");

            builder.HasKey(e => e.Id);

            builder.HasIndex(e => e.Name)
                .IsUnique()
                .HasDatabaseName("IX_PromptTemplate_Name");

            builder.HasIndex(e => new { e.Category, e.IsActive })
                .HasDatabaseName("IX_PromptTemplate_Category_Active");

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Template)
                .IsRequired();

            builder.Property(e => e.Category)
                .HasMaxLength(50)
                .HasDefaultValue("General");

            builder.Property(e => e.Variables)
                .HasColumnType("jsonb"); // PostgreSQL JSON

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
