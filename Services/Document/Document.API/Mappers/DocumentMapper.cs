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
        CreateMap<DocumentFile, DocumentDraftResponse>()
            .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().Id.ToString()))
            .ForMember(dest => dest.VersionName, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().VersionName))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().Status.ToString()))
            .ForMember(dest => dest.Summary, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().Summary))
            .ForMember(dest => dest.FileName, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().FileName))
            .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().FilePath))
            .ForMember(dest => dest.FileType, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().FileType))
            .ForMember(dest => dest.FileSize, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().FileSize.ToString()))
            .ForMember(dest => dest.CreatedTime, opt => opt.MapFrom(src => src.CreatedTime))
            .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.DocumentVersions.FirstOrDefault().DocumentTags.Select(dt => dt.Tag.Name).ToList()));

        CreateMap<DocumentVersion, PendingDocumentResponse>()
            .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.Id.ToString()));

        CreateMap<DocumentVersion, DocumentResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.DocumentFile.Id.ToString()))
            .ForMember(dest => dest.DepartmentId, opt => opt.MapFrom(src => src.DocumentFile.DepartmentId))
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.DocumentFile.Title))
            .ForMember(dest => dest.DocumentName, opt => opt.MapFrom(src => src.FileName))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.DocumentFile.Description))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.DocumentFile.CreatedBy))
            .ForMember(dest => dest.CreatedTime, opt => opt.MapFrom(src => src.DocumentFile.CreatedTime))
            .ForMember(dest => dest.LastUpdatedby, opt => opt.MapFrom(src => src.DocumentFile.LastUpdatedBy))
            .ForMember(dest => dest.LastUpdatedTime, opt => opt.MapFrom(src => src.DocumentFile.LastUpdatedTime))
            .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.FilePath))
            .ForMember(dest => dest.FileType, opt => opt.MapFrom(src => src.FileType))
            .ForMember(dest => dest.FileSize, opt => opt.MapFrom(src => src.FileSize))
            .ForMember(dest => dest.Version, opt => opt.MapFrom(src => src.VersionName))
            .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.DocumentTags.Select(dt => dt.Tag.Name).ToList()));

        CreateMap<DocumentVersion, DocumentDraftResponse>()
            .ForMember(dest => dest.DocumentId, opt => opt.MapFrom(src => src.DocumentFile.Id.ToString()))
            .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.Id.ToString()))
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.DocumentFile.Title))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.DocumentFile.Description))
            .ForMember(dest => dest.Summary, opt => opt.MapFrom(src => src.Summary))
            .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.FilePath))
            .ForMember(dest => dest.FileName, opt => opt.MapFrom(src => src.FileName))
            .ForMember(dest => dest.FileSize, opt => opt.MapFrom(src => src.FileSize))
            .ForMember(dest => dest.FileType, opt => opt.MapFrom(src => src.FileType))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.VersionName, opt => opt.MapFrom(src => src.VersionName))
            .ForMember(dest => dest.DepartmentId, opt => opt.MapFrom(src => src.DocumentFile.DepartmentId))
            .ForMember(dest => dest.OwnerId, opt => opt.MapFrom(src => src.DocumentFile.OwnerId))
            .ForMember(dest => dest.CreatedTime, opt => opt.MapFrom(src => src.DocumentFile.CreatedTime))
            .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.DocumentTags.Select(dt => dt.Tag.Name).ToList()));

        CreateMap<Tag, TagResponse>();
    }
}