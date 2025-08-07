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

    public Expression<Func<DocumentVersion, bool>> ToExpression()
    {
        return documentVersion =>
            // Official documents only
            documentVersion.IsOfficial &&
            
            // Content search filters
            (string.IsNullOrEmpty(Title) || documentVersion.Title.Contains(Title)) &&
            (string.IsNullOrEmpty(Keyword) || 
             documentVersion.Title.Contains(Keyword) ||
             documentVersion.Summary.Contains(Keyword) ||
             documentVersion.VersionName.Contains(Keyword)) &&
            (string.IsNullOrEmpty(VersionName) || documentVersion.VersionName.Contains(VersionName)) &&

            // Date filters
            (!FromDate.HasValue || documentVersion.CreatedTime >= FromDate.Value) &&
            (!ToDate.HasValue || documentVersion.CreatedTime <= ToDate.Value) &&
            (!EffectiveFrom.HasValue || documentVersion.EffectiveFrom >= EffectiveFrom.Value) &&
            (!EffectiveUntil.HasValue || documentVersion.EffectiveUntil <= EffectiveUntil.Value) &&
            (!LastSubmittedFrom.HasValue || documentVersion.LastSubmitted >= LastSubmittedFrom.Value) &&
            (!LastSubmittedTo.HasValue || documentVersion.LastSubmitted <= LastSubmittedTo.Value) &&

            // Document metadata filters
            (string.IsNullOrEmpty(DocumentTypeId) || documentVersion.DocumentFile.DocumentTypeId == DocumentTypeId) &&
            (Tags == null || !Tags.Any() || documentVersion.DocumentTags.Any(tag => Tags.Contains(tag.Tag.Name))) &&
            (string.IsNullOrEmpty(SignedBy) || documentVersion.SignedBy.Contains(SignedBy)) &&
            (string.IsNullOrEmpty(FileType) || documentVersion.FileType == FileType) &&
            (string.IsNullOrEmpty(SubmittedBy) || documentVersion.SubmittedBy == SubmittedBy) &&

            // Access control filters
            (!IsPublic.HasValue || documentVersion.IsPublic == IsPublic.Value) &&
            (string.IsNullOrEmpty(DepartmentId) || documentVersion.DocumentFile.DepartmentId == DepartmentId) &&

            // File size filters
            (!MinFileSize.HasValue || documentVersion.FileSize >= MinFileSize.Value) &&
            (!MaxFileSize.HasValue || documentVersion.FileSize <= MaxFileSize.Value) &&

            // Download count filters
            (!MinDownloads.HasValue || documentVersion.TotalDownloads >= MinDownloads.Value) &&
            (!MaxDownloads.HasValue || documentVersion.TotalDownloads <= MaxDownloads.Value);
    }
}
