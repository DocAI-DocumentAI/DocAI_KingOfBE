using Document.Domain.Models;
using System.Linq.Expressions;
using Document.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Document.Infrastructure.Filter
{
    /// <summary>
    /// Enhanced filter for approval queue with comprehensive filtering capabilities for department managers
    /// </summary>
    public class ApprovalQueueFilter : IFilter<DocumentVersion>
    {
        /// <summary>
        /// Filter by editor/submitter within the manager's department
        /// </summary>
        [MaxLength(50, ErrorMessage = "ID người dùng không được vượt quá 50 ký tự")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "ID người dùng chỉ được chứa chữ cái, số, dấu gạch ngang và gạch dưới")]
        public string? SubmittedBy { get; set; }

        /// <summary>
        /// Filter by status: pending, approved, rejected, archived
        /// </summary>
        public StatusEnum? Status { get; set; }

        /// <summary>
        /// Filter by submission date range - from date
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Filter by submission date range - to date
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Filter by document type
        /// </summary>
        [MaxLength(50, ErrorMessage = "ID loại tài liệu không được vượt quá 50 ký tự")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "ID loại tài liệu chỉ được chứa chữ cái, số, dấu gạch ngang và gạch dưới")]
        public string? DocumentTypeId { get; set; }

        /// <summary>
        /// Filter by visibility - true for public documents, false for private documents
        /// </summary>
        public bool? IsPublic { get; set; }

        /// <summary>
        /// Filter by which manager approved/rejected the document (handled separately via ApprovalLog.CreatedBy)
        /// </summary>
        [MaxLength(50, ErrorMessage = "ID người phê duyệt không được vượt quá 50 ký tự")]
        [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "ID người phê duyệt chỉ được chứa chữ cái, số, dấu gạch ngang và gạch dưới")]
        public string? ReviewedBy { get; set; }

        /// <summary>
        /// Filter by document title (partial match)
        /// </summary>
        [MaxLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự")]
        [RegularExpression(@"^[a-zA-ZÀ-ỹ0-9\s\.\-_,;:!?\(\)\[\]""'']+$", ErrorMessage = "Tiêu đề chứa ký tự không hợp lệ")]
        public string? Title { get; set; }

        /// <summary>
        /// Backward compatibility - maps to SubmittedBy
        /// </summary>
        [Obsolete("Use SubmittedBy instead")]
        public string? UserId
        {
            get => SubmittedBy;
            set => SubmittedBy = value;
        }

        /// <summary>
        /// Backward compatibility - maps to FromDate
        /// </summary>
        [Obsolete("Use FromDate instead")]
        public DateTime? From
        {
            get => FromDate;
            set => FromDate = value;
        }

        /// <summary>
        /// Backward compatibility - maps to ToDate
        /// </summary>
        [Obsolete("Use ToDate instead")]
        public DateTime? To
        {
            get => ToDate;
            set => ToDate = value;
        }

        public Expression<Func<DocumentVersion, bool>> ToExpression()
        {
            return doc =>
                // Filter by submitter/editor
                (string.IsNullOrEmpty(SubmittedBy) || doc.SubmittedBy == SubmittedBy) &&

                // Filter by status
                (!Status.HasValue || doc.Status == Status.Value) &&

                // Filter by submission date range
                (!FromDate.HasValue || doc.LastSubmitted >= FromDate.Value) &&
                (!ToDate.HasValue || doc.LastSubmitted <= ToDate.Value) &&

                // Filter by document type
                (string.IsNullOrEmpty(DocumentTypeId) || doc.DocumentFile.DocumentTypeId == DocumentTypeId) &&

                // Filter by visibility (public/private)
                (!IsPublic.HasValue || doc.IsPublic == IsPublic.Value) &&

                // Filter by document title (partial match, case-insensitive)
                (string.IsNullOrEmpty(Title) || doc.DocumentFile.Title.ToLower().Contains(Title.ToLower()));
        }
    }
}