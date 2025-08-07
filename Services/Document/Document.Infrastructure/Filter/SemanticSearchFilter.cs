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
    public List<string>? Tags { get; set; }
    public string? SignedBy { get; set; }
    public string? DocumentTypeId { get; set; }

    // Access control filters - handled internally, not exposed to users
    public string? DepartmentId { get; set; }
    public bool? IsPublic { get; set; }

    public Expression<Func<DocumentVersion, bool>> ToExpression()
    {
        return documentVersion =>
            // Date filters
            (!FromDate.HasValue || documentVersion.CreatedTime >= FromDate.Value) &&
            (!ToDate.HasValue || documentVersion.CreatedTime <= ToDate.Value) &&
            (!EffectiveFrom.HasValue || documentVersion.EffectiveFrom >= EffectiveFrom.Value) &&
            (!EffectiveUntil.HasValue || documentVersion.EffectiveUntil <= EffectiveUntil.Value) &&

            // Content filters
            (Tags == null || !Tags.Any() || documentVersion.DocumentTags.Any(tag => Tags.Contains(tag.Tag.Name))) &&
            (string.IsNullOrEmpty(SignedBy) || documentVersion.SignedBy.Contains(SignedBy)) &&
            (string.IsNullOrEmpty(DocumentTypeId) || documentVersion.DocumentFile.DocumentTypeId == DocumentTypeId) &&

            // Access control filters (handled internally)
            (string.IsNullOrEmpty(DepartmentId) || documentVersion.DocumentFile.DepartmentId == DepartmentId) &&
            (!IsPublic.HasValue || documentVersion.IsPublic == IsPublic.Value);
    }
}
