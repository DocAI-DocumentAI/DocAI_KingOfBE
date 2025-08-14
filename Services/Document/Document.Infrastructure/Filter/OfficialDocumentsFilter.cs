using Document.Domain.Models;
using Document.Domain.Enums;
using System.Linq.Expressions;

namespace Document.Infrastructure.Filter;

/// <summary>
/// Filter for official documents with comprehensive filtering capabilities
/// Follows project patterns and maintains department-based access control
/// </summary>
public class OfficialDocumentsFilter : IFilter<DocumentVersion>
{
    // Content search filters
    public string? Title { get; set; }
    public string? Keyword { get; set; }
    public string? VersionName { get; set; }

    // Date filters
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public DateTime? LastSubmittedFrom { get; set; }
    public DateTime? LastSubmittedTo { get; set; }

    // Document metadata filters
    public string? DocumentTypeId { get; set; }
    public List<string>? Tags { get; set; }
    public string? SignedBy { get; set; }
    public string? FileType { get; set; }
    public string? SubmittedBy { get; set; }

    // Access control filters (handled internally)
    public bool? IsPublic { get; set; }
    public string? DepartmentId { get; set; }

    // File size filters
    public long? MinFileSize { get; set; }
    public long? MaxFileSize { get; set; }

    // Download count filters
    public int? MinDownloads { get; set; }
    public int? MaxDownloads { get; set; }

    // Department filtering control
    public bool DepartmentOnly { get; set; } = false;

    public Expression<Func<DocumentVersion, bool>> ToExpression()
    {
        return documentVersion =>
            // Official documents only
            documentVersion.IsOfficial &&
            
            // Content search filters
            (string.IsNullOrEmpty(Title) || documentVersion.Title.ToLower().Contains(Title.ToLower())) &&
            (string.IsNullOrEmpty(Keyword) ||
            documentVersion.Title.ToLower().Contains(Keyword.ToLower()) ||
            documentVersion.Summary.ToLower().Contains(Keyword.ToLower()) ||
            documentVersion.VersionName.ToLower().Contains(Keyword.ToLower())) &&
            (string.IsNullOrEmpty(VersionName) || documentVersion.VersionName.ToLower().Contains(VersionName.ToLower())) &&


            // Date filters
            (!FromDate.HasValue || documentVersion.CreatedTime >= FromDate) &&
            (!ToDate.HasValue || documentVersion.CreatedTime <= ToDate) &&
            (!EffectiveFrom.HasValue || documentVersion.EffectiveFrom >= EffectiveFrom) &&
            (!EffectiveUntil.HasValue || documentVersion.EffectiveUntil <= EffectiveUntil) &&
            (!LastSubmittedFrom.HasValue || documentVersion.LastSubmitted >= LastSubmittedFrom) &&
            (!LastSubmittedTo.HasValue || documentVersion.LastSubmitted <= LastSubmittedTo) &&

            // Document metadata filters
            (string.IsNullOrEmpty(DocumentTypeId) || documentVersion.DocumentFile.DocumentTypeId == DocumentTypeId) &&
            (Tags == null || !Tags.Any() || documentVersion.DocumentTags.Any(docTag => Tags.Any(filterTag => docTag.Tag.Name.ToLower() == filterTag.ToLower()))) &&
            (string.IsNullOrEmpty(SignedBy) || documentVersion.SignedBy.ToLower().Contains(SignedBy.ToLower())) &&
            (string.IsNullOrEmpty(FileType) || documentVersion.FileType == FileType) &&
            (string.IsNullOrEmpty(SubmittedBy) || documentVersion.SubmittedBy.ToLower() == SubmittedBy.ToLower()) &&

            // Access control filters
            (!IsPublic.HasValue || documentVersion.IsPublic == IsPublic) &&
            (string.IsNullOrEmpty(DepartmentId) || documentVersion.DocumentFile.DepartmentId == DepartmentId) &&

            // File size filters
            (!MinFileSize.HasValue || documentVersion.FileSize >= MinFileSize) &&
            (!MaxFileSize.HasValue || documentVersion.FileSize <= MaxFileSize) &&

            // Download count filters
            (!MinDownloads.HasValue || documentVersion.TotalDownloads >= MinDownloads) &&
            (!MaxDownloads.HasValue || documentVersion.TotalDownloads <= MaxDownloads);
    }
}
