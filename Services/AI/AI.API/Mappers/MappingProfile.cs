using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.Domain.Models;
using AutoMapper;

namespace AI.API.Mappers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<SystemConfiguration, ConfigurationResponse>();

            CreateMap<AIModelConfig, AIModelConfigResponse>();
            CreateMap<GenerateRequest, AIRequest>();
            CreateMap<GenerateRequest, AIContextRequest>();

            CreateMap<SetAIModelConfigRequest, AIModelConfig>().ReverseMap();

            CreateMap<AIModelConfiguration, AIModel>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ModelId))
                .ForMember(dest => dest.Provider, opt => opt.MapFrom(src => src.ProviderType.ToString()))
                .ForMember(dest => dest.IsAvailable, opt => opt.MapFrom(src => src.IsEnabled))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
                .ForMember(dest => dest.Endpoint, opt => opt.MapFrom(src => src.Endpoint))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.ApiVersion, opt => opt.MapFrom(src => src.ApiVersion))
                .ForMember(dest => dest.LastUsed, opt => opt.MapFrom(src => src.LastUsedAt))
                .ForMember(dest => dest.LastTested, opt => opt.MapFrom(src => src.LastTestedAt))
                // These will be set by service logic
                .ForMember(dest => dest.SupportsTextGeneration, opt => opt.Ignore())
                .ForMember(dest => dest.SupportsStreaming, opt => opt.Ignore())
                .ForMember(dest => dest.SupportsEmbedding, opt => opt.Ignore())
                .ForMember(dest => dest.MaxTokens, opt => opt.Ignore())
                .ForMember(dest => dest.SupportedLanguages, opt => opt.Ignore())
                .ForMember(dest => dest.SupportsSystemPrompt, opt => opt.Ignore())
                .ForMember(dest => dest.SupportsFunctionCalling, opt => opt.Ignore())
                .ForMember(dest => dest.SupportsDocumentAnalysis, opt => opt.Ignore())
                .ForMember(dest => dest.AverageResponseTime, opt => opt.Ignore())
                .ForMember(dest => dest.TotalRequests, opt => opt.Ignore())
                .ForMember(dest => dest.SuccessRate, opt => opt.Ignore());

            CreateMap<AIModelConfiguration, ModelCapabilities>()
                .ForMember(dest => dest.SupportsTextGeneration, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.SupportsStreaming, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.SupportsEmbedding, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.SupportsSystemPrompt, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.SupportsDocumentAnalysis, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.MaxTokens, opt => opt.MapFrom(src => GetMaxTokensByProvider(src.ProviderType, src.ModelId)))
                .ForMember(dest => dest.SupportsFunctionCalling, opt => opt.MapFrom(src =>
                    src.ProviderType == AIProviderType.OpenAI || src.ProviderType == AIProviderType.MistralAI))
                .ForMember(dest => dest.SupportedLanguages, opt => opt.MapFrom(src =>
                    new List<string> { "vi", "en", "zh", "ja", "ko", "fr", "de", "es" }));

            CreateMap<AIRequestLog, AIRequestLogResponse>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.RequestId, opt => opt.MapFrom(src => src.RequestId))
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
                .ForMember(dest => dest.SourceService, opt => opt.MapFrom(src => src.SourceService))
                .ForMember(dest => dest.ModelType, opt => opt.MapFrom(src => src.ModelType.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.CompletedAt, opt => opt.MapFrom(src => src.CompletedAt))
                .ForMember(dest => dest.TokensUsed, opt => opt.MapFrom(src => src.TokensUsed))
                .ForMember(dest => dest.ResponseTimeMs, opt => opt.MapFrom(src => src.ResponseTimeMs))
                .ForMember(dest => dest.ErrorMessage, opt => opt.MapFrom(src => src.ErrorMessage));

            CreateMap<UsageMetric, UsageMetricResponse>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.RequestId, opt => opt.MapFrom(src => src.RequestId))
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
                .ForMember(dest => dest.SourceService, opt => opt.MapFrom(src => src.SourceService))
                .ForMember(dest => dest.ModelType, opt => opt.MapFrom(src => src.ModelType.ToString()))
                .ForMember(dest => dest.TokensUsed, opt => opt.MapFrom(src => src.TokensUsed))
                .ForMember(dest => dest.ResponseTimeMs, opt => opt.MapFrom(src => src.ResponseTimeMs))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.ErrorMessage, opt => opt.MapFrom(src => src.ErrorMessage))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.EstimatedCost, opt => opt.MapFrom(src => src.EstimatedCost));


            CreateMap<AIModelConfiguration, ModelPerformance>()
                .ForMember(dest => dest.AverageResponseTime, opt => opt.MapFrom(src => src.AverageResponseTime ?? 0))
                .ForMember(dest => dest.TotalRequests, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.SuccessRate, opt => opt.MapFrom(src => src.IsTestedSuccessfully ? 100.0 : 0.0))
                .ForMember(dest => dest.LastUsed, opt => opt.MapFrom(src => src.LastUsedAt))
                .ForMember(dest => dest.LastTested, opt => opt.MapFrom(src => src.LastTestedAt));

        }
        private static int GetMaxTokensByProvider(AIProviderType providerType, string modelId)
        {
            return providerType switch
            {
                AIProviderType.OpenAI when modelId.Contains("gpt-4") => 8192,
                AIProviderType.OpenAI => 4096,
                AIProviderType.HuggingFace => 4096,
                AIProviderType.MistralAI => 8192,
                AIProviderType.GoogleGemini => 32768,
                AIProviderType.AzureAIInference => 4096,
                _ => 2048
            };
        }
    }
}
