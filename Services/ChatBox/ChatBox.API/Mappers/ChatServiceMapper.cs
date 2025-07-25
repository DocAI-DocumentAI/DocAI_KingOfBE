using AutoMapper;
using ChatBox.API.Payload.Request.ChatService;
using ChatBox.API.Payload.Request.ConversationOrchestrationService;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using ChatBox.Domain.Models;
using ChatBox.Domain.Enum;

namespace ChatBox.API.Mappers
{
    public class ChatServiceMapper : Profile
    {
        public ChatServiceMapper()
        {
            // ChatSession mappings
            CreateMap<CreateSessionRequest, ChatSession>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastActivityAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => SessionStatus.Active))
                .ForMember(dest => dest.MessageCount, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.IsDeleted, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.SessionType, opt => opt.MapFrom(src => "Standard"))
                .ForMember(dest => dest.InitialContext, opt => opt.MapFrom(src => string.Empty))
                .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
                .ForMember(dest => dest.DeletionReason, opt => opt.Ignore());

            CreateMap<ChatSession, AdvancedSessionResponse>()
                .ForMember(dest => dest.Statistics, opt => opt.Ignore()) // Will be set separately
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => new Dictionary<string, object>()));

            CreateMap<ChatSession, SessionSummaryResponse>()
                .ForMember(dest => dest.LastMessage, opt => opt.MapFrom(src => string.Empty))
                .ForMember(dest => dest.Duration, opt => opt.MapFrom(src => src.LastActivityAt - src.CreatedAt));

            // ChatMessage mappings
            CreateMap<SendMessageRequest, ChatMessage>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.SessionId, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.Content, opt => opt.MapFrom(src => src.Message))
                .ForMember(dest => dest.AiResponse, opt => opt.Ignore())
                .ForMember(dest => dest.MessageType, opt => opt.MapFrom(src => ChatBox.Domain.Enum.MessageType.Text))
                .ForMember(dest => dest.TokensUsed, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.SourceDocuments, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
                .ForMember(dest => dest.DeletionReason, opt => opt.Ignore())
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => SerializeContext(src.Context)));

            CreateMap<ChatMessage, AdvancedMessageResponse>()
                .ForMember(dest => dest.Response, opt => opt.MapFrom(src => src.AiResponse))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.MessageType))
                .ForMember(dest => dest.Sources, opt => opt.Ignore()) // Will be set separately
                .ForMember(dest => dest.Feedback, opt => opt.Ignore()) // Will be set separately
                .ForMember(dest => dest.Metadata, opt => opt.Ignore()); // Will be set separately

            CreateMap<ChatMessage, SendMessageResponse>()
                .ForMember(dest => dest.Success, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.Message, opt => opt.MapFrom(src => "Message sent successfully"))
                .ForMember(dest => dest.MessageId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Response, opt => opt.MapFrom(src => src.AiResponse))
                .ForMember(dest => dest.Sources, opt => opt.MapFrom(src => new List<DocumentReference>()))
                .ForMember(dest => dest.SuggestedQuestions, opt => opt.MapFrom(src => new List<string>()))
                .ForMember(dest => dest.TokensUsed, opt => opt.MapFrom(src => src.TokensUsed))
                .ForMember(dest => dest.ProcessingTime, opt => opt.MapFrom(src => TimeSpan.Zero))
                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => new Dictionary<string, object>()));

            // ConversationResponse to SendMessageResponse mapping
            CreateMap<ConversationResponse, SendMessageResponse>()
                .ForMember(dest => dest.Sources, opt => opt.MapFrom(src => src.DocumentReferences));

            // SendMessageRequest to ProcessMessageRequest mapping
            CreateMap<SendMessageRequest, ProcessMessageRequest>()
                .ForMember(dest => dest.UserId, opt => opt.Ignore()) // Will be set separately
                .ForMember(dest => dest.IpAddress, opt => opt.Ignore()) // Will be set separately
                .ForMember(dest => dest.UserAgent, opt => opt.Ignore()) // Will be set separately
                .ForMember(dest => dest.Context, opt => opt.MapFrom(src => src.Context));

            // MessageFeedback mappings
            CreateMap<FeedbackRequest, MessageFeedback>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.MessageId, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // Search mappings
            CreateMap<SearchRequest, object>()
                .ConvertUsing(src => new
                {
                    Query = src.Query,
                    Page = src.Page,
                    Size = src.Size,
                    FromDate = src.FromDate,
                    ToDate = src.ToDate,
                    SessionIds = src.SessionIds
                });
        }

        private static string SerializeContext(Dictionary<string, object>? context)
        {
            if (context == null || context.Count == 0)
                return string.Empty;

            try
            {
                return System.Text.Json.JsonSerializer.Serialize(context);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
