using Document.Domain.Models;
using Document.Domain.Enums;
using System.Linq.Expressions;

namespace Document.Infrastructure.Filter;

public class SemanticSearchFilter : IFilter<DocumentVersion>
{
    // Date filters - business relevant
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }

    // Content filters - business relevant
    public string? DocumentTypeId { get; set; }

    // Access control filters - handled internally, not exposed to users
    public string? DepartmentId { get; set; }
    public bool? IsPublic { get; set; }

    // Folder-based filters
    public string? FolderId { get; set; }
    public bool IncludeSubfolders { get; set; } = false;
    public string? FolderPath { get; set; }

    public Expression<Func<DocumentVersion, bool>> ToExpression()
    {
        return documentVersion =>
            // Date filters
            (!FromDate.HasValue || documentVersion.CreatedTime >= FromDate) &&
            (!ToDate.HasValue || documentVersion.CreatedTime <= ToDate) &&
            (!EffectiveFrom.HasValue || documentVersion.EffectiveFrom >= EffectiveFrom) &&
            (!EffectiveUntil.HasValue || documentVersion.EffectiveUntil <= EffectiveUntil) &&

            // Content filters
            (string.IsNullOrEmpty(DocumentTypeId) || documentVersion.DocumentFile.DocumentTypeId == DocumentTypeId) &&

            // Access control filters (handled internally)
            (string.IsNullOrEmpty(DepartmentId) || documentVersion.DocumentFile.DepartmentId == DepartmentId) &&
            (!IsPublic.HasValue || documentVersion.IsPublic == IsPublic) &&

            // Folder-based filtering
            (string.IsNullOrEmpty(FolderId) || documentVersion.FolderId == FolderId ||
             (IncludeSubfolders && documentVersion.Folder != null && documentVersion.Folder.FullPath.StartsWith(
                 documentVersion.Folder.FullPath + "/"))) &&
            (string.IsNullOrEmpty(FolderPath) || (documentVersion.Folder != null &&
             documentVersion.Folder.FullPath.ToLower().Contains(FolderPath.ToLower())));
    }
}
