using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AI.Domain.Configuration;
using Microsoft.EntityFrameworkCore;

namespace AI.Domain.Models
{
    public class DocAIDbContext : DbContext
    {
        public DocAIDbContext(DbContextOptions<DocAIDbContext> options) : base(options) { }

        public DbSet<SystemConfiguration> SystemConfigurations { get; set; }
        public DbSet<UsageMetric> UsageMetrics { get; set; }
        public DbSet<AIRequestLog> AIRequestLogs { get; set; }
        public DbSet<AIModelConfiguration> AIModelConfigurations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new SystemConfigurationConfig());

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            base.OnModelCreating(modelBuilder);
        }
     
    }
}
