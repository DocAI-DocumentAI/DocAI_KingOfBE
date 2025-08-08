using Document.Domain.Enums;
using Document.Domain.Models;
using System.Linq.Expressions;

namespace Document.Infrastructure.Filter;

/// <summary>
/// Filter for listing documents that can be replaced, following business rules
/// </summary>
public class ReplaceableDocumentsFilter : IFilter<DocumentVersion>
{
    // Content filters
    public string? Title { get; set; }
    public string? Keyword { get; set; }

    // Date filters
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    // Metadata filters
    public string? DocumentTypeId { get; set; }
    public List<string>? Tags { get; set; }
    public string? SignedBy { get; set; }

    // Access control (internal)
    public string? DepartmentId { get; set; }

    public Expression<Func<DocumentVersion, bool>> ToExpression()
    {
        return v =>
            // Business rules for replaceable documents:
            // - Latest approved version exists
            // - Document is official or approved
            // - Document is not already being replaced
            v.Status == StatusEnum.Approved &&
            v.IsOfficial &&
            !v.DocumentFile.IsReplaced &&
            
            // Access control: public or same department
            (v.IsPublic || (DepartmentId == null || v.DocumentFile.DepartmentId == DepartmentId)) &&
            
            // Content filters
            (string.IsNullOrEmpty(Title) || v.Title.Contains(Title)) &&
            (string.IsNullOrEmpty(Keyword) || v.Title.Contains(Keyword) || v.Summary.Contains(Keyword)) &&

            // Date filters
            (!FromDate.HasValue || v.CreatedTime >= FromDate.Value) &&
            (!ToDate.HasValue || v.CreatedTime <= ToDate.Value) &&
            
            // Metadata filters
            (string.IsNullOrEmpty(DocumentTypeId) || v.DocumentFile.DocumentTypeId == DocumentTypeId) &&
            (Tags == null || !Tags.Any() || v.DocumentTags.Any(t => Tags.Contains(t.Tag.Name))) &&
            (string.IsNullOrEmpty(SignedBy) || v.SignedBy!.Contains(SignedBy));
    }
}
