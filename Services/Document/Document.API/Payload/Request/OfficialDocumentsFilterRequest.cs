using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Document.API.Payload.Request;

/// <summary>
/// Request model for filtering official documents with comprehensive filtering capabilities
/// Follows project validation patterns and maintains department-based access control
/// </summary>
public class OfficialDocumentsFilterRequest
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
    /// Filter by version name (partial match)
    /// </summary>
    [FromQuery]
    [StringLength(100, ErrorMessage = "Tên phiên bản không được vượt quá {1} ký tự")]
    public string? VersionName { get; set; }

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
    /// Filter documents effective from this date
    /// </summary>
    [FromQuery]
    public DateTime? EffectiveFrom { get; set; }

    /// <summary>
    /// Filter documents effective until this date
    /// </summary>
    [FromQuery]
    public DateTime? EffectiveUntil { get; set; }

    /// <summary>
    /// Filter documents submitted from this date
    /// </summary>
    [FromQuery]
    public DateTime? LastSubmittedFrom { get; set; }

    /// <summary>
    /// Filter documents submitted until this date
    /// </summary>
    [FromQuery]
    public DateTime? LastSubmittedTo { get; set; }

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
    /// Filter by file type (e.g., PDF, DOCX)
    /// </summary>
    [FromQuery]
    [StringLength(10, ErrorMessage = "Loại file không được vượt quá {1} ký tự")]
    public string? FileType { get; set; }

    /// <summary>
    /// Filter by user who submitted the document
    /// </summary>
    [FromQuery]
    [StringLength(50, ErrorMessage = "ID người gửi không được vượt quá {1} ký tự")]
    public string? SubmittedBy { get; set; }

    /// <summary>
    /// Filter by public/private status
    /// </summary>
    [FromQuery]
    public bool? IsPublic { get; set; }

    /// <summary>
    /// Minimum file size in bytes
    /// </summary>
    [FromQuery]
    [Range(0, long.MaxValue, ErrorMessage = "Kích thước file tối thiểu phải lớn hơn hoặc bằng 0")]
    public long? MinFileSize { get; set; }

    /// <summary>
    /// Maximum file size in bytes
    /// </summary>
    [FromQuery]
    [Range(0, long.MaxValue, ErrorMessage = "Kích thước file tối đa phải lớn hơn hoặc bằng 0")]
    public long? MaxFileSize { get; set; }

    /// <summary>
    /// Minimum download count
    /// </summary>
    [FromQuery]
    [Range(0, int.MaxValue, ErrorMessage = "Số lượt tải tối thiểu phải lớn hơn hoặc bằng 0")]
    public int? MinDownloads { get; set; }

    /// <summary>
    /// Maximum download count
    /// </summary>
    [FromQuery]
    [Range(0, int.MaxValue, ErrorMessage = "Số lượt tải tối đa phải lớn hơn hoặc bằng 0")]
    public int? MaxDownloads { get; set; }

    /// <summary>
    /// Filter to show only documents from user's department (both public and private)
    /// If false, shows public documents from all departments
    /// </summary>
    [FromQuery]
    public bool DepartmentOnly { get; set; } = false;
}
