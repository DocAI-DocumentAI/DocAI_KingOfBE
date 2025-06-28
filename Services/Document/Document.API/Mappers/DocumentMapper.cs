using AutoMapper;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Domain.Model;
using Document.Domain.Models;

namespace Document.API.Mappers;

public class DocumentMapper : Profile
{
    public DocumentMapper()
    {
        CreateMap<UploadDocumentDraftRequest, DocumentFile>();
        CreateMap<DocumentFile, DocumentResponse>()
            .ForMember(dest => dest.FilePath, opt => opt.Ignore())
            .ForMember(dest => dest.FileType, opt => opt.Ignore())
            .ForMember(dest => dest.FileSize, opt => opt.Ignore())
            .ForMember(dest => dest.Version, opt => opt.Ignore());
            //.ForMember(dest => dest.Text, opt => opt.Ignore());
        CreateMap<UpdateMetaDataReqest, DocumentFile>();
        CreateMap<DocumentFile, DocumentFileResponse>();
        CreateMap<DocumentFile, DocumentDraftResponse>()
            .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().Id.ToString()))
            .ForMember(dest => dest.VersionName, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().VersionName))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().Status.ToString()))
            .ForMember(dest => dest.Sumary, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().Summary))
            .ForMember(dest => dest.FileName, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().FileName))
            .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().FilePath))
            .ForMember(dest => dest.FileType, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().FileType))
            .ForMember(dest => dest.FileSize, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().FileSize.ToString()))
            .ForMember(dest => dest.CreatedTime, opt => opt.MapFrom(src => src.CreatedTime));

        CreateMap<DocumentVersion, PendingDocumentResponse>()
            .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.Id.ToString()));
    }
}