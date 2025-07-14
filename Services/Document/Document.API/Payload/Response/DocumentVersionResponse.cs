namespace Document.API.Payload.Response
{
    public class DocumentVersionResponse
    {
        public string Id { get; set; }
        public string VersionNumber { get; set; }
        public string DocumentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public List<string> Tags { get; set; }
        public DateTime? LastSubmitted { get; set; }
        public string? SubmittedBy { get; set; }
    }
}
