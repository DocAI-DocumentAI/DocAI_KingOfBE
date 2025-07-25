namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentStatusResponse
    {
        public string DocumentId { get; set; }
        public string Status { get; set; }
        public bool IsAccessible { get; set; }
        public string StatusReason { get; set; }
        public DateTime LastStatusCheck { get; set; }
        public DocumentLifecycleInfo LifecycleInfo { get; set; }
        public List<string> AvailableVersions { get; set; } = new();
        public Dictionary<string, object> StatusMetadata { get; set; } = new();
    }
}
