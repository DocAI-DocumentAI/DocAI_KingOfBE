
using AutoMapper;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.Domain.Models;
using Shared.DTOs;

namespace ChatBox.API.Mappers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ChatSession, SessionResponse>()
                         .ForMember(dest => dest.MessageCount, opt => opt.Ignore())
                         .ForMember(dest => dest.IsModelActive, opt => opt.Ignore()); // Set trong service

            CreateMap<ChatSession, SessionDetailResponse>()
                .ForMember(dest => dest.IsModelActive, opt => opt.Ignore()); // Set trong service

            CreateMap<CreateSessionRequest, ChatSession>();
            CreateMap<ChatMessage, MessageResponse>();
            CreateMap<UserPreference, PreferenceResponse>();
            CreateMap<UpdatePreferenceRequest, UserPreference>();

            // AI Configuration mappings - sử dụng mapping hiện tại
            CreateMap<AIConfiguration, AIConfigurationResponse>();
            CreateMap<AIConfigurationRequest, AIConfiguration>();

            CreateMap<UserPreferenceRequest, UserPreference>();

            // New mappings for admin functionality
            CreateMap<SetMultipleModelsRequest, List<string>>()
                .ConvertUsing(src => src.ModelNames);

            CreateMap<ChatBoxDocumentSource, DocumentInfo>();

        }
    }
}
