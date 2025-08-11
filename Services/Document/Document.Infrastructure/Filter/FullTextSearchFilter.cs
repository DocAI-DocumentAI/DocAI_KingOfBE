
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
             documentVersion.Title.ToLower().Contains(Keyword.ToLower()) ||
             documentVersion.Summary.ToLower().Contains(Keyword.ToLower()) ||
             documentVersion.VersionName.ToLower().Contains(Keyword.ToLower())) &&
            (!FromDate.HasValue || documentVersion.CreatedTime >= FromDate) &&
            (!ToDate.HasValue || documentVersion.CreatedTime <= ToDate) &&
            (Tags == null || !Tags.Any() || documentVersion.DocumentTags.Any(docTag => Tags.Any(filterTag => docTag.Tag.Name.ToLower() == filterTag.ToLower()))) &&
            (string.IsNullOrEmpty(DepartmentId) || documentVersion.DocumentFile.DepartmentId == DepartmentId) &&
            (!IsPublic.HasValue || documentVersion.IsPublic == IsPublic) &&
            (string.IsNullOrEmpty(SignedBy) || documentVersion.SignedBy.ToLower().Contains(SignedBy.ToLower())) &&
            (string.IsNullOrEmpty(DocumentTypeId) || documentVersion.DocumentFile.DocumentTypeId == DocumentTypeId);
    }
}
