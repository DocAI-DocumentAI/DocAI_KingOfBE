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
            // System Configuration mappings
            CreateMap<SystemConfiguration, ConfigurationResponse>();

            // AI Model Configuration mappings
            CreateMap<AIModelConfig, AIModelConfigResponse>();
            CreateMap<SetAIModelConfigRequest, AIModelConfig>();

            // AI Request Log mappings
            CreateMap<AIRequestLog, AIRequestLogResponse>();

            // Usage Metric mappings
            CreateMap<UsageMetric, UsageMetricResponse>();
        }
    }
}
