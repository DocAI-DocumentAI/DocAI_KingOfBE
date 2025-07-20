using System.Text.Json;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.Domain.Enums;
using AI.Domain.Models;
using AutoMapper;

namespace AI.API.Mappers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {

            // System Configuration Mappings
            CreateMap<SystemConfiguration, ConfigurationResponse>();
            CreateMap<UpdateConfigurationRequest, SystemConfiguration>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

            // Model Configuration Mappings - Fixed
            CreateMap<ModelConfiguration, ModelConfigurationResponse>()
                .ForMember(dest => dest.ModelType, opt => opt.MapFrom(src => src.ModelType.ToString()))
                .ForMember(dest => dest.ProviderName, opt => opt.MapFrom(src => src.ModelProvider != null ? src.ModelProvider.Name : "Unknown"))
                .ForMember(dest => dest.Success, opt => opt.MapFrom(src => true));

            CreateMap<UpdateModelConfigurationRequest, ModelConfiguration>()
                .ForMember(dest => dest.ModelType, opt => opt.MapFrom(src => Enum.Parse<ModelType>(src.ModelType, true)))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ModelProviderId, opt => opt.Ignore())
                .ForMember(dest => dest.ModelProvider, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

            // Model Provider Mappings
            CreateMap<ModelProvider, ModelProviderResponse>()
                .ForMember(dest => dest.Models, opt => opt.MapFrom(src => src.ModelConfigurations ?? new List<ModelConfiguration>()))
                .ForMember(dest => dest.Success, opt => opt.MapFrom(src => true));

            // Prompt Template Mappings - Fixed for expression tree error
            CreateMap<PromptTemplate, PromptTemplateResponse>()
                .ForMember(dest => dest.Success, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.Variables, opt => opt.Ignore()) // Will handle manually
                .AfterMap((src, dest) =>
                {
                    dest.Variables = string.IsNullOrEmpty(src.Variables)
                        ? new Dictionary<string, string>()
                        : JsonSerializer.Deserialize<Dictionary<string, string>>(src.Variables) ?? new Dictionary<string, string>();
                });

            CreateMap<CreatePromptTemplateRequest, PromptTemplate>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Variables, opt => opt.Ignore()) // Will handle manually
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .AfterMap((src, dest) =>
                {
                    dest.Variables = JsonSerializer.Serialize(src.Variables ?? new Dictionary<string, string>());
                });

            CreateMap<PromptTemplate, PromptTemplateSummary>()
                .ForMember(dest => dest.VariableCount, opt => opt.Ignore()) // Will handle manually
                .AfterMap((src, dest) =>
                {
                    if (!string.IsNullOrEmpty(src.Variables))
                    {
                        try
                        {
                            var vars = JsonSerializer.Deserialize<Dictionary<string, string>>(src.Variables);
                            dest.VariableCount = vars?.Count ?? 0;
                        }
                        catch
                        {
                            dest.VariableCount = 0;
                        }
                    }
                });

            // Usage Metric Mappings
            CreateMap<UsageMetric, UsageMetricResponse>()
                .ForMember(dest => dest.ModelType, opt => opt.MapFrom(src => src.ModelType.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            // AI Request Log Mappings - Fixed
            CreateMap<AIRequestLog, AIRequestLogResponse>()
                .ForMember(dest => dest.ModelType, opt => opt.MapFrom(src => src.ModelType.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.RequestContent, opt => opt.Ignore())
                .ForMember(dest => dest.ResponseContent, opt => opt.Ignore())
                .AfterMap((src, dest) =>
                {
                    try
                    {
                        dest.RequestContent = string.IsNullOrEmpty(src.RequestContent)
                            ? null
                            : JsonSerializer.Deserialize<object>(src.RequestContent);
                        dest.ResponseContent = string.IsNullOrEmpty(src.ResponseContent)
                            ? null
                            : JsonSerializer.Deserialize<object>(src.ResponseContent);
                    }
                    catch
                    {
                        dest.RequestContent = src.RequestContent;
                        dest.ResponseContent = src.ResponseContent;
                    }
                });

            //    // Document Context to Domain Document
            //    CreateMap<DocumentContext, Document>()
            //        .ForMember(dest => dest.DocumentId, opt => opt.MapFrom(src => src.DocumentId))
            //        .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.VersionId))
            //        .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
            //        .ForMember(dest => dest.Content, opt => opt.MapFrom(src => src.Content))
            //        .ForMember(dest => dest.TypeName, opt => opt.MapFrom(src => src.TypeName))
            //        .ForMember(dest => dest.VersionCode, opt => opt.MapFrom(src => src.VersionCode))
            //        .ForMember(dest => dest.Summary, opt => opt.MapFrom(src => src.Summary))
            //        .ForMember(dest => dest.DepartmentId, opt => opt.MapFrom(src => src.DepartmentId))
            //        .ForMember(dest => dest.SignedBy, opt => opt.MapFrom(src => src.SignedBy))
            //        .ForMember(dest => dest.EffectiveFrom, opt => opt.MapFrom(src => src.EffectiveFrom))
            //        .ForMember(dest => dest.EffectiveUntil, opt => opt.MapFrom(src => src.EffectiveUntil))
            //        .ForMember(dest => dest.IsPublic, opt => opt.MapFrom(src => src.IsPublic))
            //        .ForMember(dest => dest.RelevanceScore, opt => opt.MapFrom(src => src.RelevanceScore))
            //        .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.FilePath))
            //        .ForMember(dest => dest.FileType, opt => opt.MapFrom(src => src.FileType))
            //        .ForMember(dest => dest.FileSize, opt => opt.MapFrom(src => src.FileSize));
            //}

        }
    }
}
