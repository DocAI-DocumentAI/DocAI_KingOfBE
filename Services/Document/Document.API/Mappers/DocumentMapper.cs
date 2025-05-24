using AutoMapper;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Domain.Model;

namespace Document.API.Mappers;

public class DocumentMapper : Profile
{
    public DocumentMapper()
    {
        CreateMap<UploadDocumentRequest, DocumentFile>();
        CreateMap<DocumentFile, DocumentResponse>()
            .ForMember(dest => dest.FilePath, opt => opt.Ignore())
            .ForMember(dest => dest.FileType, opt => opt.Ignore())
            .ForMember(dest => dest.FileSize, opt => opt.Ignore())
            .ForMember(dest => dest.Version, opt => opt.Ignore())
            .ForMember(dest => dest.Text, opt => opt.Ignore());
        CreateMap<UpdateMetaDataReqest, DocumentFile>();
        CreateMap<DocumentFile, DocumentFileResponse>();
    }
}