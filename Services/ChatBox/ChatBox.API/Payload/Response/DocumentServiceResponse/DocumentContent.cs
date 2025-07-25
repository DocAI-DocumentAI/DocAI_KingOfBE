namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentContent
    {
        public string DocumentId { get; set; }
        public string Content { get; set; }
        public string ContentType { get; set; }
        public List<DocumentChunk> Chunks { get; set; } = new();
        public Dictionary<string, object> ProcessingMetadata { get; set; } = new();
    }
}
