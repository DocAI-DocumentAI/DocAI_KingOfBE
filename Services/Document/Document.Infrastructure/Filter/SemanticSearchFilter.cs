using Document.Domain.Models;
using System.Linq.Expressions;

namespace Document.Infrastructure.Filter;

public class SemanticSearchFilter : IFilter<DocumentVersion>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<string>? Tags { get; set; }
    public string? DepartmentId { get; set; }
    public bool? IsPublic { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public string? SignedBy { get; set; }

    public Expression<Func<DocumentVersion, bool>> ToExpression()
    {
        return documentVersion =>
            (!FromDate.HasValue || documentVersion.CreatedTime >= FromDate.Value) &&
            (!ToDate.HasValue || documentVersion.CreatedTime <= ToDate.Value) &&
            (Tags == null || !Tags.Any() || documentVersion.DocumentTags.Any(tag => Tags.Contains(tag.Tag.Name))) &&
            (string.IsNullOrEmpty(DepartmentId) || documentVersion.DocumentFile.DepartmentId == DepartmentId);
    }
}
