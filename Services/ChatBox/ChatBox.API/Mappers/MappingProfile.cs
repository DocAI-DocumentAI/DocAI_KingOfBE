using AutoMapper;
using ChatBox.API.Payload.Response;
using ChatBox.Domain.Models;

namespace ChatBox.API.Mappers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Ánh xạ Conversation Model sang ConversationResponse DTO
            CreateMap<Conversation, ConversationResponse>()
                .ForMember(dest => dest.Messages, opt => opt.Ignore()); // Ignore Messages để populate thủ công nếu cần

            // Ánh xạ Conversation Model sang ConversationSummaryResponse DTO
            CreateMap<Conversation, ConversationSummaryResponse>();

            // Ánh xạ MessageHistory Model sang MessageResponse DTO
            CreateMap<MessageHistory, MessageResponse>()
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.SenderRole))
                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => src.CreateAt))
                .ForMember(dest => dest.Order, opt => opt.MapFrom(src => src.Order)); // REVIEW POINT: Ánh xạ Order

            // Ánh xạ MessageResponse DTO sang MessageHistory Model (cho LimitConversationHistory)
            CreateMap<MessageResponse, MessageHistory>()
                .ForMember(dest => dest.SenderRole, opt => opt.MapFrom(src => src.Role))
                .ForMember(dest => dest.CreateAt, opt => opt.MapFrom(src => src.Timestamp)); // REVIEW POINT: Ánh xạ ngược CreateAt
        }
    }
}
