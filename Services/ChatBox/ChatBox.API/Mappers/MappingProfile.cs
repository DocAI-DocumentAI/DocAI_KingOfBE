using AutoMapper;
using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Request.UserPreferenceService;
using ChatBox.API.Payload.Request.AIClientService;
using ChatBox.API.Payload.Request.DocumentClientService;
using ChatBox.API.Payload.Response;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.UserPreferenceResponse;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Response.DocumentServiceResponse;
using ChatBox.API.Payload.Response.AnalyticsResponse;
using ChatBox.Domain.Models;
using ChatBox.Domain.Enum;

namespace ChatBox.API.Mappers
{
    /// <summary>
    /// Main AutoMapper profile that includes all mapping configurations
    /// </summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Include all specific mapper profiles
            // Note: IncludeProfile is not available in this version, so we'll add mappings directly

            // Additional common mappings that don't fit in specific mappers
            ConfigureCommonMappings();
            ConfigureEnumMappings();
            ConfigureUtilityMappings();
        }

        /// <summary>
        /// Configure common mappings used across multiple services
        /// </summary>
        private void ConfigureCommonMappings()
        {
            // Base entity mappings
            CreateMap<DateTime, DateTime>().ConvertUsing(src => src.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(src, DateTimeKind.Utc) : src);

            // Generic response mappings
            CreateMap<object, object>()
                .ConvertUsing(src => new
                {
                    Message = "An error occurred",
                    CorrelationId = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow
                });

            // Pagination mappings
            CreateMap(typeof(ChatBox.Infrastructure.Paginate.IPaginate<>), typeof(ChatBox.Infrastructure.Paginate.IPaginate<>));
        }

        /// <summary>
        /// Configure enum mappings
        /// </summary>
        private void ConfigureEnumMappings()
        {
            // Basic enum mappings
            CreateMap<SessionStatus, string>().ConvertUsing(src => src.ToString());
            CreateMap<MessageType, string>().ConvertUsing(src => src.ToString());
            CreateMap<ContentSeverity, string>().ConvertUsing(src => src.ToString());
            CreateMap<AccessLevel, string>().ConvertUsing(src => src.ToString());
        }

        /// <summary>
        /// Configure utility mappings for common data transformations
        /// </summary>
        private void ConfigureUtilityMappings()
        {
            // Basic type mappings
            CreateMap<Guid, string>().ConvertUsing(src => src.ToString());
            CreateMap<TimeSpan, string>().ConvertUsing(src => src.ToString());
        }
    }
}
