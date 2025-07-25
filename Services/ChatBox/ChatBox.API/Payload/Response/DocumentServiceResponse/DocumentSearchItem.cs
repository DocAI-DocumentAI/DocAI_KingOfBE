namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentSearchItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public double RelevanceScore { get; set; }
        public DateTime LastModified { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public string Author { get; set; }
    }
}
