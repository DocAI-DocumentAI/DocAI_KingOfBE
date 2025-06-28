namespace Document.API.Payload.Response
{
    public class PendingDocumentResponse
    {
        public string VersionId { get; set; }
        public string VersionName { get; set; }
        public string Title { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public string DepartmentId { get; set; }
        public string DepartmentName { get; set; }
    }
}
