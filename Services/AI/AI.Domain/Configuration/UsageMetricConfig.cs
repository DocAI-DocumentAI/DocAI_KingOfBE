using AI.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI.Domain.Configuration
{
    public class UsageMetricConfig : IEntityTypeConfiguration<UsageMetric>
    {
        public void Configure(EntityTypeBuilder<UsageMetric> builder)
        {
            builder.ToTable("UsageMetrics");
            
            builder.HasKey(x => x.Id);
            
            builder.Property(x => x.RequestId)
                .IsRequired()
                .HasMaxLength(100);
                
            builder.Property(x => x.SourceService)
                .HasMaxLength(100);
                
            builder.Property(x => x.ModelType)
                .HasMaxLength(100);
                
            builder.Property(x => x.Status)
                .HasMaxLength(50);
                
            builder.Property(x => x.ErrorMessage)
                .HasMaxLength(1000);
                
            builder.Property(x => x.EstimatedCost)
                .HasColumnType("decimal(18,6)");
                
            builder.HasIndex(x => x.RequestId);
            builder.HasIndex(x => x.CreatedAt);
            builder.HasIndex(x => x.SourceService);
        }
    }
}
