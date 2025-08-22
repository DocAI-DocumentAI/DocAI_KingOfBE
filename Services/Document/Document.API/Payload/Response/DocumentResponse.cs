namespace Document.API.Payload.Response;

/// <summary>
/// Response model for document information
/// </summary>
public class DocumentResponse
{
    /// <summary>
    /// Unique identifier for the document
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Department ID associated with the document
    /// </summary>
    public string? DepartmentId { get; set; }

    /// <summary>
    /// Department name associated with the document
    /// </summary>
    public string? DepartmentName { get; set; }

    /// <summary>
    /// Title of the document
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Name of the document file
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the document
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Current status of the document
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// User who created the document
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Name of user who created the document
    /// </summary>
    public string? CreatedByName { get; set; }

    /// <summary>
    /// When the document was created
    /// </summary>
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// User who last updated the document
    /// </summary>
    public string? LastUpdatedby { get; set; }

    /// <summary>
    /// Name of user who last updated the document
    /// </summary>
    public string? LastUpdatedByName { get; set; }

    /// <summary>
    /// When the document was last updated
    /// </summary>
    public DateTime? LastUpdatedTime { get; set; }

    /// <summary>
    /// File path of the document
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Type of the document file
    /// </summary>
    public string? FileType { get; set; }

    /// <summary>
    /// Size of the document file in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Version of the document
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Tags associated with the document
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Document type ID
    /// </summary>
    public string DocumentTypeId { get; set; } = string.Empty;

    /// <summary>
    /// Document type name
    /// </summary>
    public string? DocumentTypeName { get; set; }

    /// <summary>
    /// ID of the document that replaces this one
    /// </summary>
    public string? ReplacementId { get; set; }
    public DocumentResponse? ReplacementDocument { get; set; }

    /// <summary>
    /// Whether this document has been replaced
    /// </summary>
    public bool IsReplaced { get; set; }

    /// <summary>
    /// ID of the document that replaces this document (reverse relationship)
    /// </summary>
    public string? ReplacedById { get; set; }

    /// <summary>
    /// Full details of the document that replaces this document (reverse relationship)
    /// </summary>
    public DocumentResponse? ReplacedByDocument { get; set; }

    /// <summary>
    /// Name of the document that replaces this document (reverse relationship)
    /// </summary>
    public string? ReplacedByDocumentName { get; set; }

    /// <summary>
    /// Indicates whether the document is public (accessible to all employees) or private (restricted to same department)
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Person or authority who signed the document
    /// </summary>
    public string? SignedBy { get; set; }
    
    public DateTime? EffectiveFrom { get; set; }

        /// <summary>
        /// Effective date until which the document is valid
        /// </summary>
    public DateTime? EffectiveUntil { get; set; }
}
