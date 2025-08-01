using AutoMapper;
using Document.API.Payload.Response;
using Document.Domain.Models;

namespace Document.API.Mappers
{
    public class ApprovalQueueDetailMapper : Profile
    {
        public ApprovalQueueDetailMapper()
        {
            CreateMap<DocumentVersion, ApprovalQueueDetailResponse>()
                .ForMember(dest => dest.DocumentId, opt => opt.MapFrom(src => src.DocumentFile.Id))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.Id))
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
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.DocumentTags.Select(dt => dt.Tag.Name).ToList()))
                .ForMember(dest => dest.CreatedTime, opt => opt.MapFrom(src => src.DocumentFile.CreatedTime))
                .ForMember(dest => dest.ReplacementId, opt => opt.MapFrom(src => src.DocumentFile.ReplacementId))
                .ForMember(dest => dest.ReplacementDocument, opt => opt.MapFrom(src => src.DocumentFile.ReplacementDocument))
                .ForMember(dest => dest.IsReplaced, opt => opt.MapFrom(src => src.DocumentFile.IsReplaced))
                .ForMember(dest => dest.LastSubmitted, opt => opt.MapFrom(src => src.LastSubmitted))
                .ForMember(dest => dest.SubmittedBy, opt => opt.MapFrom(src => src.SubmittedBy))
                .ForMember(dest => dest.ClaimedBy, opt => opt.MapFrom(src => src.ApprovalClaim.ClaimedBy))
                .ForMember(dest => dest.ClaimedAt, opt => opt.MapFrom(src => src.ApprovalClaim.ClaimedAt))
                .ForMember(dest => dest.DocumentTypeId, opt => opt.MapFrom(src => src.DocumentFile.DocumentTypeId))
                .ForMember(dest => dest.DocumentTypeName, opt => opt.MapFrom(src => src.DocumentFile.DocumentType != null ? src.DocumentFile.DocumentType.Name : null));
        }
    }
}