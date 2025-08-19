
using Document.Domain.Models;
using System.Linq.Expressions;

namespace Document.Infrastructure.Filter;

public class FullTextSearchFilter : IFilter<DocumentVersion>
{
    public string? Keyword { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<string>? Tags { get; set; }
    public string? DepartmentId { get; set; }
    public bool? IsPublic { get; set; }
    public string? SignedBy { get; set; }
    public string? DocumentTypeId { get; set; }

    /// <summary>
    /// Filter by specific folder ID
    /// </summary>
    public string? FolderId { get; set; }

    /// <summary>
    /// Include documents from subfolders
    /// </summary>
    public bool IncludeSubfolders { get; set; } = false;

    /// <summary>
    /// Filter by folder path (supports partial matching)
    /// </summary>
    public string? FolderPath { get; set; }

    public Expression<Func<DocumentVersion, bool>> ToExpression()
    {
        return documentVersion =>
            (string.IsNullOrEmpty(Keyword) ||
             documentVersion.Title.ToLower().Contains(Keyword.ToLower()) ||
             documentVersion.Summary.ToLower().Contains(Keyword.ToLower()) ||
             documentVersion.VersionName.ToLower().Contains(Keyword.ToLower())) &&
            (!FromDate.HasValue || documentVersion.CreatedTime >= FromDate) &&
            (!ToDate.HasValue || documentVersion.CreatedTime <= ToDate) &&
            (Tags == null || !Tags.Any() || documentVersion.DocumentTags.Any(docTag => Tags.Any(filterTag => docTag.Tag.Name.ToLower() == filterTag.ToLower()))) &&
            (string.IsNullOrEmpty(DepartmentId) || documentVersion.DocumentFile.DepartmentId == DepartmentId) &&
            (!IsPublic.HasValue || documentVersion.IsPublic == IsPublic) &&
            (string.IsNullOrEmpty(SignedBy) || documentVersion.SignedBy.ToLower().Contains(SignedBy.ToLower())) &&
            (string.IsNullOrEmpty(DocumentTypeId) || documentVersion.DocumentFile.DocumentTypeId == DocumentTypeId) &&
            // Folder-based filtering
            (string.IsNullOrEmpty(FolderId) || documentVersion.FolderId == FolderId ||
             (IncludeSubfolders && documentVersion.Folder != null && documentVersion.Folder.FullPath.StartsWith(
                 documentVersion.Folder.FullPath + "/"))) &&
            (string.IsNullOrEmpty(FolderPath) || (documentVersion.Folder != null &&
             documentVersion.Folder.FullPath.ToLower().Contains(FolderPath.ToLower())));
    }
}
