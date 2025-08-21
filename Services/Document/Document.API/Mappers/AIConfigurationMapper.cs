using AutoMapper;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Domain.Models;

namespace Document.API.Mappers;

/// <summary>
/// AutoMapper profile for AI Configuration mappings
/// </summary>
public class AIConfigurationMapper : Profile
{
    public AIConfigurationMapper()
    {
        // Request to Domain Entity
        CreateMap<CreateAIConfigurationRequest, AIConfiguration>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedTime, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.LastUpdatedTime, opt => opt.Ignore())
            .ForMember(dest => dest.LastUpdatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedTime, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedBy, opt => opt.Ignore());

        CreateMap<UpdateAIConfigurationRequest, AIConfiguration>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedTime, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.LastUpdatedTime, opt => opt.Ignore())
            .ForMember(dest => dest.LastUpdatedBy, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedTime, opt => opt.Ignore())
            .ForMember(dest => dest.DeletedBy, opt => opt.Ignore());

        // Domain Entity to Response
        CreateMap<AIConfiguration, AIConfigurationResponse>()
            .ForMember(dest => dest.CreatedByName, opt => opt.Ignore()) // Will be enriched
            .ForMember(dest => dest.LastUpdatedByName, opt => opt.Ignore()); // Will be enriched
    }
}
