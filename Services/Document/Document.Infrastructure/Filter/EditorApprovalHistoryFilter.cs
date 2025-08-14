using Document.Domain.Enums;
using Document.Domain.Models;
using System.Linq.Expressions;

namespace Document.Infrastructure.Filter
{
    /// <summary>
    /// Filter for editor's approval history (approved/rejected documents)
    /// </summary>
    public class EditorApprovalHistoryFilter : IFilter<DocumentVersion>
    {
        /// <summary>
        /// Filter by document title (partial match)
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Search keyword in title, summary, and version name (partial match)
        /// </summary>
        public string? Keyword { get; set; }

        /// <summary>
        /// Filter by document status (Approved, Rejected, Archived)
        /// </summary>
        public StatusEnum? Status { get; set; }

        /// <summary>
        /// Filter documents created from this date
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Filter documents created until this date
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Filter documents reviewed from this date
        /// </summary>
        public DateTime? ReviewedFromDate { get; set; }

        /// <summary>
        /// Filter documents reviewed until this date
        /// </summary>
        public DateTime? ReviewedToDate { get; set; }

        /// <summary>
        /// Filter by document type ID
        /// </summary>
        public string? DocumentTypeId { get; set; }

        /// <summary>
        /// Filter by tags
        /// </summary>
        public List<string>? Tags { get; set; }

        /// <summary>
        /// Filter by person who signed the document (partial match)
        /// </summary>
        public string? SignedBy { get; set; }

        /// <summary>
        /// Filter by manager who reviewed the document
        /// </summary>
        public string? ReviewedBy { get; set; }

        public Expression<Func<DocumentVersion, bool>> ToExpression()
        {
            return documentVersion =>
                // Content search filters
                (string.IsNullOrEmpty(Title) || documentVersion.Title.ToLower().Contains(Title.ToLower())) &&
                (string.IsNullOrEmpty(Keyword) ||
                 documentVersion.Title.ToLower().Contains(Keyword.ToLower()) ||
                 documentVersion.Summary.ToLower().Contains(Keyword.ToLower()) ||
                 documentVersion.VersionName.ToLower().Contains(Keyword.ToLower())) &&

                // Status filter (only approved, rejected, archived)
                (!Status.HasValue || documentVersion.Status == Status.Value) &&

                // Date filters
                (!FromDate.HasValue || documentVersion.CreatedTime >= FromDate.Value) &&
                (!ToDate.HasValue || documentVersion.CreatedTime <= ToDate.Value) &&
                (!ReviewedFromDate.HasValue || documentVersion.LastUpdatedTime >= ReviewedFromDate.Value) &&
                (!ReviewedToDate.HasValue || documentVersion.LastUpdatedTime <= ReviewedToDate.Value) &&

                // Document metadata filters
                (string.IsNullOrEmpty(DocumentTypeId) || documentVersion.DocumentFile.DocumentTypeId == DocumentTypeId) &&
                (Tags == null || !Tags.Any() || documentVersion.DocumentTags.Any(docTag => Tags.Any(filterTag => docTag.Tag.Name.ToLower() == filterTag.ToLower()))) &&
                (string.IsNullOrEmpty(SignedBy) || documentVersion.SignedBy.ToLower().Contains(SignedBy.ToLower())) &&
                (string.IsNullOrEmpty(ReviewedBy) || documentVersion.LastUpdatedBy.ToLower() == ReviewedBy.ToLower());
        }
    }
}
