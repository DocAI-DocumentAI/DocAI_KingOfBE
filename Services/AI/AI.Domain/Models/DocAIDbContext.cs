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
        public DbSet<ModelConfiguration> ModelConfigurations { get; set; }
        public DbSet<ModelProvider> ModelProviders { get; set; }
        public DbSet<UsageMetric> UsageMetrics { get; set; }
        public DbSet<PromptTemplate> PromptTemplates { get; set; }
        public DbSet<AIRequestLog> AIRequestLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply configurations
            modelBuilder.ApplyConfiguration(new SystemConfigurationConfig());
            modelBuilder.ApplyConfiguration(new ModelConfigurationConfig());
            modelBuilder.ApplyConfiguration(new ModelProviderConfig());
            modelBuilder.ApplyConfiguration(new UsageMetricConfig());
            modelBuilder.ApplyConfiguration(new PromptTemplateConfig());
            modelBuilder.ApplyConfiguration(new AIRequestLogConfig());

            // Add foreign key for ModelConfiguration to ModelProvider
            modelBuilder.Entity<ModelConfiguration>()
                .Property<int?>("ModelProviderId");

            // Seed initial data
            SeedData(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            base.OnModelCreating(modelBuilder);
        }
        private void SeedData(ModelBuilder modelBuilder)
        {
            // Model Providers
            modelBuilder.Entity<ModelProvider>().HasData(
                new ModelProvider
                {
                    Id = 1,
                    Name = "HuggingFace",
                    BaseUrl = "https://api-inference.huggingface.co",
                    ApiKeyName = "HuggingFace:ApiKey",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );

            // System Configurations
            modelBuilder.Entity<SystemConfiguration>().HasData(
                new SystemConfiguration
                {
                    Id = 1,
                    Key = "AI:DefaultMaxTokens",
                    Value = "2048",
                    Category = "AI",
                    Description = "Default maximum tokens for AI responses",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new SystemConfiguration
                {
                    Id = 2,
                    Key = "AI:DefaultTemperature",
                    Value = "0.7",
                    Category = "AI",
                    Description = "Default temperature for AI responses",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new SystemConfiguration
                {
                    Id = 3,
                    Key = "AI:DefaultTopP",
                    Value = "0.9",
                    Category = "AI",
                    Description = "Default top_p for AI responses",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new SystemConfiguration
                {
                    Id = 4,
                    Key = "AI:EnableRequestLogging",
                    Value = "true",
                    Category = "Logging",
                    Description = "Enable logging of all AI requests and responses",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new SystemConfiguration
                {
                    Id = 5,
                    Key = "AI:MaxRetryAttempts",
                    Value = "3",
                    Category = "Resilience",
                    Description = "Maximum retry attempts for failed AI requests",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );

            modelBuilder.Entity<PromptTemplate>().HasData(
           new PromptTemplate
           {
               Id = 1,
               Name = "DefaultRAG",
               Template = @"You are a helpful AI assistant specialized in searching internal documents.

                    Instructions:
                    - Answer based ONLY on the provided documents
                    - If information is not in the documents, say 'I cannot find this information in the provided documents'
                    - Always cite the source document(s) used
                    - Be accurate and concise
                    - Respond in the same language as the question

                    Context Documents:
                    {documents}

                    Question: {question}

                    Answer:",
               Category = "RAG",
               IsActive = true,
               Variables = "{\"documents\":\"\",\"question\":\"\"}",
               CreatedAt = DateTime.UtcNow,
               UpdatedAt = DateTime.UtcNow
           },
           new PromptTemplate
           {
               Id = 2,
               Name = "SimpleChat",
               Template = @"You are a helpful AI assistant.{system_prompt} User: {message}"
           }); 
        }
    }
}
