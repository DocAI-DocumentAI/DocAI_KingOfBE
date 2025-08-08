using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request;

/// <summary>
/// Request model for listing documents that can be replaced
/// </summary>
public class ReplaceableDocumentsFilterRequest
{
    [FromQuery]
    [StringLength(200)]
    public string? Title { get; set; }

    [FromQuery]
    [StringLength(500)]
    public string? Keyword { get; set; }

    [FromQuery]
    public DateTime? FromDate { get; set; }

    [FromQuery]
    public DateTime? ToDate { get; set; }

    [FromQuery]
    [StringLength(50)]
    public string? DocumentTypeId { get; set; }

    [FromQuery]
    public List<string>? Tags { get; set; }

    [FromQuery]
    [StringLength(200)]
    public string? SignedBy { get; set; }
}
