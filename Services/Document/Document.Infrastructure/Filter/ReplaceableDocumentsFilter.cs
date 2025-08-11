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



    public Expression<Func<DocumentVersion, bool>> ToExpression()
    {
        return v =>
    // Content filters
    (string.IsNullOrEmpty(Title) || v.Title.ToLower().Contains(Title.ToLower())) &&
    (string.IsNullOrEmpty(Keyword) || 
        v.Title.ToLower().Contains(Keyword.ToLower()) || 
        v.Summary.ToLower().Contains(Keyword.ToLower())) &&

    // Date filters
    (!FromDate.HasValue || v.CreatedTime >= FromDate.Value) &&
    (!ToDate.HasValue || v.CreatedTime <= ToDate.Value) &&

    // Metadata filters
    (string.IsNullOrEmpty(DocumentTypeId) || v.DocumentFile.DocumentTypeId.ToLower() == DocumentTypeId.ToLower()) &&
    (Tags == null || !Tags.Any() || v.DocumentTags.Any(t => Tags.Any(tag => tag.ToLower() == t.Tag.Name.ToLower()))) &&
    // Safe null-check and case-insensitive search
    (string.IsNullOrEmpty(SignedBy) || (v.SignedBy != null && v.SignedBy.ToLower().Contains(SignedBy.ToLower())));
    }
}
