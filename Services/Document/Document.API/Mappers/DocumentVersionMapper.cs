using AutoMapper;
using Document.API.Payload.Response;
using Document.Domain.Models;

namespace Document.API.Mappers
{
    public class DocumentVersionMapper : Profile
    {
        public DocumentVersionMapper()
        {
            CreateMap<DocumentVersion, DocumentVersionResponse>()
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.DocumentTags.Select(dt => dt.Tag.Name).ToList()))
                .ForMember(dest => dest.LastSubmitted, opt => opt.MapFrom(src => src.LastSubmitted))
                .ForMember(dest => dest.SubmittedBy, opt => opt.MapFrom(src => src.SubmittedBy));

            CreateMap<DocumentVersion, PendingDocumentResponse>()
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.DepartmentId, opt => opt.MapFrom(src => src.DocumentFile.DepartmentId));
        }
    }
}
