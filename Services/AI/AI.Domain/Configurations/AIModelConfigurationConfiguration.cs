using AI.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI.Domain.Configurations
{
    public class AIModelConfigurationConfiguration : IEntityTypeConfiguration<AIModelConfiguration>
    {
        public void Configure(EntityTypeBuilder<AIModelConfiguration> builder)
        {
            builder.ToTable("AIModelConfigurations");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.ModelId)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.ApiKey)
                .HasMaxLength(500);

            builder.Property(x => x.Endpoint)
                .HasMaxLength(500);

            builder.Property(x => x.OrganizationId)
                .HasMaxLength(100);

            builder.Property(x => x.ApiVersion)
                .HasMaxLength(50);

            builder.Property(x => x.LastTestError)
                .HasMaxLength(1000);

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.Property(x => x.ProviderType)
                .IsRequired()
                .HasConversion<int>();

            // Seed data - 6 model configurations (chỉ HuggingFace có thông tin sẵn, các model khác cần admin nhập)
            builder.HasData(
                new AIModelConfiguration
                {
                    Id = 1,
                    Name = "OpenAI GPT-4",
                    ProviderType = AIProviderType.OpenAI,
                    ModelId = "gpt-4",
                    Endpoint = "https://api.openai.com/v1",
                    Description = "OpenAI GPT-4 model for advanced text generation - Requires API Key configuration",
                    IsEnabled = true,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new AIModelConfiguration
                {
                    Id = 2,
                    Name = "OpenAI GPT-3.5 Turbo",
                    ProviderType = AIProviderType.OpenAI,
                    ModelId = "gpt-3.5-turbo",
                    Endpoint = "https://api.openai.com/v1",
                    Description = "OpenAI GPT-3.5 Turbo model for fast text generation - Requires API Key configuration",
                    IsEnabled = true,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new AIModelConfiguration
                {
                    Id = 3,
                    Name = "Mistral Large",
                    ProviderType = AIProviderType.MistralAI,
                    ModelId = "mistral-large-latest",
                    Endpoint = "https://api.mistral.ai/v1",
                    Description = "Mistral AI Large model for high-quality text generation - Requires API Key configuration",
                    IsEnabled = true,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new AIModelConfiguration
                {
                    Id = 4,
                    Name = "Google Gemini Pro",
                    ProviderType = AIProviderType.GoogleGemini,
                    ModelId = "gemini-pro",
                    Endpoint = "https://generativelanguage.googleapis.com/v1beta",
                    Description = "Google Gemini Pro model for versatile text generation - Requires API Key configuration",
                    IsEnabled = true,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new AIModelConfiguration
                {
                    Id = 5,
                    Name = "Azure AI Inference",
                    ProviderType = AIProviderType.AzureAIInference,
                    ModelId = "gpt-4",
                    Description = "Azure AI Inference service for enterprise text generation - Requires API Key and Endpoint configuration",
                    IsEnabled = true,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new AIModelConfiguration
                {
                    Id = 6,
                    Name = "HuggingFace Fallback",
                    ProviderType = AIProviderType.HuggingFace,
                    ModelId = "moonshotai/Kimi-K2-Instruct",
                    ApiKey = "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",
                    Endpoint = "https://router.huggingface.co/v1/chat/completions",
                    Description = "HuggingFace fallback model - Pre-configured and ready to use",
                    IsEnabled = true,
                    IsTestedSuccessfully = true,
                    IsActive = true,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            // Index for performance
            builder.HasIndex(x => x.IsActive);
            builder.HasIndex(x => x.IsEnabled);
            builder.HasIndex(x => x.IsTestedSuccessfully);
            builder.HasIndex(x => new { x.IsActive, x.IsTestedSuccessfully });
        }
    }
}
