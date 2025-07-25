namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentLifecycleInfo
    {
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime? ReviewDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public bool IsLatestVersion { get; set; }
        public string ReplacedBy { get; set; }
        public List<string> PreviousVersions { get; set; } = new();
    }
}
