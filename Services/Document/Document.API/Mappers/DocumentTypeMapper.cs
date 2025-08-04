using AutoMapper;
using Document.API.Payload.Response;
using Document.Domain.Models;

namespace Document.API.Mappers
{
    public class DocumentTypeMapper : Profile
    {
        public DocumentTypeMapper()
        {
            CreateMap<DocumentType, DocumentTypeResponse>()
                .ForMember(dest => dest.DocumentCount, opt => opt.MapFrom(src => src.DocumentFiles.Count));
        }
    }
}
