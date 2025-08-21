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
                .ForMember(dest => dest.DocumentId, opt => opt.MapFrom(src => src.DocumentFile.Id))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.DocumentFile.Description))
                .ForMember(dest => dest.DepartmentId, opt => opt.MapFrom(src => src.DocumentFile.DepartmentId))
                .ForMember(dest => dest.OwnerId, opt => opt.MapFrom(src => src.DocumentFile.OwnerId))
                .ForMember(dest => dest.ReplacementId, opt => opt.MapFrom(src => src.DocumentFile.ReplacementId))
                .ForMember(dest => dest.ReplacementDocumentName, opt => opt.MapFrom(src => src.DocumentFile.ReplacementDocument.Title))
                .ForMember(dest => dest.ReplacementDocument, opt => opt.MapFrom(src => src.DocumentFile.ReplacementDocument))
                .ForMember(dest => dest.IsReplaced, opt => opt.MapFrom(src => src.DocumentFile.IsReplaced))
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.DocumentTags.Select(dt => dt.Tag.Name).ToList()))
                .ForMember(dest => dest.LastSubmitted, opt => opt.MapFrom(src => src.LastSubmitted))
                .ForMember(dest => dest.SubmittedBy, opt => opt.MapFrom(src => src.SubmittedBy))
                .ForMember(dest => dest.DocumentTypeId, opt => opt.MapFrom(src => src.DocumentFile.DocumentTypeId))
                .ForMember(dest => dest.DocumentTypeName, opt => opt.MapFrom(src => src.DocumentFile.DocumentType != null ? src.DocumentFile.DocumentType.Name : null))
                .ForMember(dest => dest.SignedBy, opt => opt.MapFrom(src => src.SignedBy))
                .ForMember(dest => dest.EffectiveFrom, opt => opt.MapFrom(src => src.EffectiveFrom))
                .ForMember(dest => dest.EffectiveUntil, opt => opt.MapFrom(src => src.EffectiveUntil))
                .ForMember(dest => dest.FolderId, opt => opt.MapFrom(src => src.FolderId))
                .ForMember(dest => dest.FolderName, opt => opt.MapFrom(src => src.Folder != null ? src.Folder.Name : null))
                .ForMember(dest => dest.TargetFolderId, opt => opt.MapFrom(src => src.TargetFolderId))
                .ForMember(dest => dest.TargetFolderName, opt => opt.MapFrom(src => src.TargetFolder != null ? src.TargetFolder.Name : null));

            CreateMap<DocumentVersion, PendingDocumentResponse>()
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.DepartmentId, opt => opt.MapFrom(src => src.DocumentFile.DepartmentId))
                .ForMember(dest => dest.DocumentTypeId, opt => opt.MapFrom(src => src.DocumentFile.DocumentTypeId))
                .ForMember(dest => dest.DocumentTypeName, opt => opt.MapFrom(src => src.DocumentFile.DocumentType != null ? src.DocumentFile.DocumentType.Name : null))
                .ForMember(dest => dest.SignedBy, opt => opt.MapFrom(src => src.SignedBy))
                .ForMember(dest => dest.EffectiveFrom, opt => opt.MapFrom(src => src.EffectiveFrom))
                .ForMember(dest => dest.EffectiveUntil, opt => opt.MapFrom(src => src.EffectiveUntil));
        }
    }
}
