namespace Document.API.Payload.Response;

public class DocumentResponse
{
    public string Id { get; set; }
    public string? DepartmentId { get; set; }
    public string Title { get; set; }
    public string DocumentName { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? LastUpdatedby { get; set; }
    public DateTime? LastUpdatedTime { get; set; }
    public string? FilePath { get; set; }
    public string? FileType { get; set; }
    public long FileSize { get; set; }
    public string? Version { get; set; }
}
