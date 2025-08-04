
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

    public Expression<Func<DocumentVersion, bool>> ToExpression()
    {
        return documentVersion =>
            (string.IsNullOrEmpty(Keyword) ||
             documentVersion.Title.Contains(Keyword) ||
             documentVersion.Summary.Contains(Keyword) ||
             documentVersion.VersionName.Contains(Keyword)) &&
            (!FromDate.HasValue || documentVersion.CreatedTime >= FromDate.Value) &&
            (!ToDate.HasValue || documentVersion.CreatedTime <= ToDate.Value) &&
            (Tags == null || !Tags.Any() || documentVersion.DocumentTags.Any(tag => Tags.Contains(tag.Tag.Name))) &&
            (string.IsNullOrEmpty(DepartmentId) || documentVersion.DocumentFile.DepartmentId == DepartmentId) &&
            (!IsPublic.HasValue || documentVersion.IsPublic == IsPublic.Value) &&
            (string.IsNullOrEmpty(SignedBy) || documentVersion.SignedBy.Contains(SignedBy)) &&
            (string.IsNullOrEmpty(DocumentTypeId) || documentVersion.DocumentFile.DocumentTypeId == DocumentTypeId);
    }
}
