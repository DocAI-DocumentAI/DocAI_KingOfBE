namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentMetadata
    {
        public string DocumentId { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string DocumentType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Author { get; set; }
        public long SizeBytes { get; set; }
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> CustomMetadata { get; set; } = new();
    }
}
