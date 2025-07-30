
using AutoMapper;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.Domain.Models;

namespace ChatBox.API.Mappers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ChatSession, SessionResponse>()
           .ForMember(dest => dest.MessageCount, opt => opt.Ignore());

            CreateMap<ChatSession, SessionDetailResponse>();
            CreateMap<CreateSessionRequest, ChatSession>();

            // Message mappings
            CreateMap<ChatMessage, MessageResponse>();

            // Preference mappings
            CreateMap<SessionPreference, PreferenceResponse>();
            CreateMap<UpdatePreferenceRequest, SessionPreference>();

            // AI Configuration mappings
            CreateMap<AIConfiguration, AIConfigurationResponse>();
            CreateMap<AIConfigurationRequest, AIConfiguration>();

            // Prohibited Word mappings
            CreateMap<ProhibitedWord, ProhibitedWordResponse>();
            CreateMap<ProhibitedWordRequest, ProhibitedWord>();

            CreateMap<UserPreferenceRequest, SessionPreference>();
        }
    }
}
