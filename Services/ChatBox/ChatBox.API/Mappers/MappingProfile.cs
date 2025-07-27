using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Request.UserPreferenceService;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.UserPreferenceResponse;
using ChatBox.Domain.Models;
using System.Text.Json;
using AutoMapper;
using ChatBox.API.Payload.Request.AIClientService;
using ChatBox.API.Payload.Request.ConversationOrchestrationService;
using ChatBox.API.Services.Implement;

namespace ChatBox.API.Mappers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Chat Session Mappings
            CreateMap<CreateSessionRequest, ChatSession>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.LastActivityAt, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.MessageCount, opt => opt.Ignore())
                .ReverseMap();

            CreateMap<ChatSession, SessionResponse>().ReverseMap();

            // User Preference Mappings
            CreateMap<UpdatePreferencesRequest, UserPreference>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.CustomSettings, opt => opt.Ignore())
                .ForMember(dest => dest.PreferredTopics, opt => opt.Ignore())
                .ForMember(dest => dest.BlockedTopics, opt => opt.Ignore())
                .ReverseMap();

            CreateMap<UserPreference, UserPreferenceResponse>()
                .ForMember(dest => dest.CustomSettings, opt => opt.Ignore())
                .ForMember(dest => dest.PreferredTopics, opt => opt.Ignore())
                .ForMember(dest => dest.BlockedTopics, opt => opt.Ignore())
                .ForMember(dest => dest.IsDefault, opt => opt.Ignore())
                .ReverseMap();

            // Message Feedback Mappings
            CreateMap<MessageFeedbackRequest, MessageFeedback>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.MessageId, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Metadata, opt => opt.Ignore())
                .ReverseMap();

            // Conversation Response Mappings
            CreateMap<ConversationResponse, SendMessageResponse>().ReverseMap();

            // AI Service Request/Response Mappings
            CreateMap<ProcessMessageRequest, AdvancedAiGenerationRequest>()
                .ForMember(dest => dest.Query, opt => opt.MapFrom(src => src.Message))
                .ForMember(dest => dest.Model, opt => opt.MapFrom(src => src.AIModelId))
                .ForMember(dest => dest.SystemPrompt, opt => opt.Ignore())
                .ForMember(dest => dest.Context, opt => opt.MapFrom(src => src.Context))
                .ForMember(dest => dest.ConversationHistory, opt => opt.Ignore())
                .ForMember(dest => dest.UserPreferences, opt => opt.Ignore())
                .ReverseMap();

            // Search Result Mappings
            CreateMap<ChatMessage, SearchResult>()
                .ForMember(dest => dest.MessageId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Content, opt => opt.MapFrom(src => src.Content))
                .ForMember(dest => dest.Response, opt => opt.MapFrom(src => src.AiResponse))
                .ForMember(dest => dest.RelevanceScore, opt => opt.Ignore())
                .ForMember(dest => dest.MatchContext, opt => opt.Ignore())
                .ReverseMap();

            // Document Reference Mappings
            CreateMap<AccessibleDocument, DocumentReference>()
                .ForMember(dest => dest.DocumentId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Excerpt, opt => opt.Ignore())
                .ForMember(dest => dest.Url, opt => opt.Ignore())
                .ReverseMap();
        

            CreateMap<ChatSession, SessionResponse>().ReverseMap();


            CreateMap<ChatMessage, MessageResponse>()
                 .ForMember(dest => dest.Sources, opt => opt.Ignore())
                 .ForMember(dest => dest.Metadata, opt => opt.Ignore())
                 .ForMember(dest => dest.Feedback, opt => opt.Ignore())
                 .ReverseMap();


            //// AI Model Mappings
            //CreateMap<SwitchModelRequest, ChatSession>()
            //    .ForMember(dest => dest.AIModelId, opt => opt.MapFrom(src => src.NewModelId))
            //    .ForMember(dest => dest.Temperature, opt => opt.MapFrom(src => src.Temperature))
            //    .ForMember(dest => dest.MaxTokens, opt => opt.MapFrom(src => src.MaxTokens))
            //    .ForAllOtherMembers(opt => opt.Ignore());

            // Audit Service Mappings
            CreateMap<AuditLog, AuditLogResponse>();

            CreateMap<SecurityAuditLog, SecurityAuditResponse>();

            // Security Service Mappings
            CreateMap<SecurityIncident, SecurityIncidentResponse>();

            CreateMap<UserSecurityProfile, UserSecurityProfileResponse>();

            // Rate Limiting Mappings
            CreateMap<RateLimitRule, RateLimitRuleResponse>();

            CreateMap<UserRateLimitStatus, UserRateLimitStatusResponse>();

            CreateMap<RateLimitViolation, RateLimitViolationResponse>();

            // Content Moderation Mappings
            CreateMap<ContentModerationRule, ContentModerationRuleResponse>()
                .ForMember(dest => dest.Keywords, opt => opt.MapFrom(src =>
                    ParseJsonToStringList(src.Keywords)));

            CreateMap<ModerationLog, ModerationLogResponse>()
                .ForMember(dest => dest.ViolatedRules, opt => opt.MapFrom(src =>
                    ParseJsonToStringList(src.ViolatedRules)));

            CreateMap<UserModerationHistory, UserModerationHistoryResponse>();

            // Token Validation Mappings
            CreateMap<TokenBreakdown, TokenBreakdownResponse>();

            CreateMap<ModelRecommendation, ModelRecommendationResponse>();

            CreateMap<OptimizedContent, OptimizedContentResponse>();

            // System Preference Mappings
            CreateMap<SetDefaultPreferencesRequest, SystemPreference>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.DataType, opt => opt.Ignore())
                .ForMember(dest => dest.AllowedValues, opt => opt.Ignore())
                .ForMember(dest => dest.DefaultValue, opt => opt.MapFrom(src =>
                    JsonSerializer.Serialize(src.DefaultValue)));

            CreateMap<SystemPreference, SystemPreferenceResponse>()
                .ForMember(dest => dest.DefaultValue, opt => opt.MapFrom(src =>
                    ParseJsonToObject(src.DefaultValue)))
                .ForMember(dest => dest.AllowedValues, opt => opt.MapFrom(src =>
                    ParseJsonToStringList(src.AllowedValues)));
        }

        // Helper methods for JSON parsing
        private static List<string> ParseJsonToStringList(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static object ParseJsonToObject(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new object();

            try
            {
                return JsonSerializer.Deserialize<object>(json) ?? new object();
            }
            catch
            {
                return new object();
            }
        }
    }
}
