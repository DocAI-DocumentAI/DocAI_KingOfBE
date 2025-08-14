using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Document.Domain.Enums;

namespace Document.API.Payload.Request
{
    /// <summary>
    /// Request model for filtering editor's approval history (approved/rejected documents)
    /// </summary>
    public class EditorApprovalHistoryFilterRequest
    {
        /// <summary>
        /// Filter by document title (partial match)
        /// </summary>
        [FromQuery]
        [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá {1} ký tự")]
        public string? Title { get; set; }

        /// <summary>
        /// Search keyword in title, summary, and version name (partial match)
        /// </summary>
        [FromQuery]
        [StringLength(500, ErrorMessage = "Từ khóa tìm kiếm không được vượt quá {1} ký tự")]
        public string? Keyword { get; set; }

        /// <summary>
        /// Filter by document status (Approved, Rejected, Archived)
        /// </summary>
        [FromQuery]
        public StatusEnum? Status { get; set; }

        /// <summary>
        /// Filter documents created from this date
        /// </summary>
        [FromQuery]
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Filter documents created until this date
        /// </summary>
        [FromQuery]
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Filter documents reviewed from this date
        /// </summary>
        [FromQuery]
        public DateTime? ReviewedFromDate { get; set; }

        /// <summary>
        /// Filter documents reviewed until this date
        /// </summary>
        [FromQuery]
        public DateTime? ReviewedToDate { get; set; }

        /// <summary>
        /// Filter by document type ID
        /// </summary>
        [FromQuery]
        [StringLength(50, ErrorMessage = "ID loại tài liệu không được vượt quá {1} ký tự")]
        public string? DocumentTypeId { get; set; }

        /// <summary>
        /// Filter by tags (comma-separated list)
        /// </summary>
        [FromQuery]
        public List<string>? Tags { get; set; }

        /// <summary>
        /// Filter by person who signed the document (partial match)
        /// </summary>
        [FromQuery]
        [StringLength(200, ErrorMessage = "Người ký không được vượt quá {1} ký tự")]
        public string? SignedBy { get; set; }

        /// <summary>
        /// Filter by manager who reviewed the document
        /// </summary>
        [FromQuery]
        [StringLength(50, ErrorMessage = "ID người duyệt không được vượt quá {1} ký tự")]
        public string? ReviewedBy { get; set; }
    }
}
