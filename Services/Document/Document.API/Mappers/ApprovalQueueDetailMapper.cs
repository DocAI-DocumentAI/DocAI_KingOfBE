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
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.DocumentFile.CreatedBy))
                .ForMember(dest => dest.LastUpdatedTime, opt => opt.MapFrom(src => src.LastUpdatedTime))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => src.LastUpdatedBy))
                .ForMember(dest => dest.ReplacementId, opt => opt.MapFrom(src => src.DocumentFile.ReplacementId))
                .ForMember(dest => dest.ReplacementDocument, opt => opt.MapFrom(src => src.DocumentFile.ReplacementDocument))
                .ForMember(dest => dest.ReplacementDocumentName, opt => opt.MapFrom(src => src.DocumentFile.ReplacementDocument != null ? src.DocumentFile.ReplacementDocument.Title : null))
                .ForMember(dest => dest.IsReplaced, opt => opt.MapFrom(src => src.DocumentFile.IsReplaced))
                .ForMember(dest => dest.IsPublic, opt => opt.MapFrom(src => src.IsPublic))
                .ForMember(dest => dest.LastSubmitted, opt => opt.MapFrom(src => src.LastSubmitted))
                .ForMember(dest => dest.SubmittedBy, opt => opt.MapFrom(src => src.SubmittedBy))
                .ForMember(dest => dest.ClaimedBy, opt => opt.MapFrom(src => src.ApprovalClaim != null ? src.ApprovalClaim.ClaimedBy : null))
                .ForMember(dest => dest.ClaimedAt, opt => opt.MapFrom(src => src.ApprovalClaim != null ? (DateTime?)src.ApprovalClaim.ClaimedAt : null))
                .ForMember(dest => dest.SignedBy, opt => opt.MapFrom(src => src.SignedBy))
                .ForMember(dest => dest.EffectiveFrom, opt => opt.MapFrom(src => src.EffectiveFrom))
                .ForMember(dest => dest.EffectiveUntil, opt => opt.MapFrom(src => src.EffectiveUntil))
                .ForMember(dest => dest.ReviewedBy, opt => opt.MapFrom(src => src.LastUpdatedBy))
                .ForMember(dest => dest.ReviewedAt, opt => opt.MapFrom(src => src.LastUpdatedTime))
                .ForMember(dest => dest.DocumentTypeId, opt => opt.MapFrom(src => src.DocumentFile.DocumentTypeId))
                .ForMember(dest => dest.DocumentTypeName, opt => opt.MapFrom(src => src.DocumentFile.DocumentType != null ? src.DocumentFile.DocumentType.Name : null))
                // Calculated fields - will be set in service layer
                .ForMember(dest => dest.DaysSinceSubmission, opt => opt.Ignore())
                .ForMember(dest => dest.IsApproachingExpiration, opt => opt.Ignore())
                .ForMember(dest => dest.Priority, opt => opt.Ignore())
                .ForMember(dest => dest.ResubmissionCount, opt => opt.Ignore())
                .ForMember(dest => dest.PreviousRejectionReason, opt => opt.Ignore())
                .ForMember(dest => dest.ReviewComments, opt => opt.Ignore())
                .ForMember(dest => dest.DownloadCount, opt => opt.Ignore())
                .ForMember(dest => dest.ViewCount, opt => opt.Ignore())
                // Names will be enriched by enrichment service
                .ForMember(dest => dest.DepartmentName, opt => opt.Ignore())
                .ForMember(dest => dest.OwnerName, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedByName, opt => opt.Ignore())
                .ForMember(dest => dest.LastUpdatedByName, opt => opt.Ignore())
                .ForMember(dest => dest.SubmittedByName, opt => opt.Ignore())
                .ForMember(dest => dest.ClaimedByName, opt => opt.Ignore())
                .ForMember(dest => dest.ReviewedByName, opt => opt.Ignore());
        }
    }
}