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
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.DocumentTags.Select(dt => dt.Tag.Name).ToList()));
        }
    }
}
