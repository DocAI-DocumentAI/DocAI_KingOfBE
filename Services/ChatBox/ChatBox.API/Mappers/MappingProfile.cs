using AutoMapper;
using ChatBox.API.Payload.Response;
using ChatBox.Domain.Models;

namespace ChatBox.API.Mappers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Conversation, ConversationResponse>().ReverseMap();
            CreateMap<Conversation, ConversationSummaryResponse>().ReverseMap();
            CreateMap<MessageHistory, MessageResponse>().ReverseMap();
            CreateMap<MessageResponse, MessageHistory>().ReverseMap();
        }
    }
}
