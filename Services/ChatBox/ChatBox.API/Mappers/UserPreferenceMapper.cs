using AutoMapper;
using ChatBox.API.Payload.Request.UserPreferenceService;
using ChatBox.API.Payload.Response.UserPreferenceResponse;
using ChatBox.Domain.Models;

namespace ChatBox.API.Mappers
{
    public class UserPreferenceMapper : Profile
    {
        public UserPreferenceMapper()
        {
            // UserPreference to Response mappings
            CreateMap<UserPreference, UserPreferenceResponse>();

            // Request to UserPreference mappings
            CreateMap<UpdatePreferencesRequest, UserPreference>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

            // SetDefaultPreferencesRequest to UserPreference mappings
            CreateMap<SetDefaultPreferencesRequest, UserPreference>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

            // Default preferences response mapping
            CreateMap<UserPreference, DefaultPreferencesResponse>();

            // Preference validation mapping
            CreateMap<UpdatePreferencesRequest, PreferenceValidationInfo>()
                .ForMember(dest => dest.IsValid, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.ValidationErrors, opt => opt.MapFrom(src => new List<string>()));
        }
    }
}
