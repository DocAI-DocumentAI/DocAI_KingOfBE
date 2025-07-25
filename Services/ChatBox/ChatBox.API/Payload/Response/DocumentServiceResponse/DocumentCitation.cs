namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentCitation
    {
        public string DocumentId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public float RelevanceScore { get; set; }
        public string Category { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Author { get; set; }
        public string Source { get; set; }
        public string Url { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
