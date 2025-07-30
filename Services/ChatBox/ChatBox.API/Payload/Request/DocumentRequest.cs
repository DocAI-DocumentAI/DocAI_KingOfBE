namespace ChatBox.API.Payload.Request
{
    public class DocumentRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string Query { get; set; } = string.Empty; // User's question/query
        public string UserId { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 5;
        public double MinRelevanceScore { get; set; } = 0.7;
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        public bool OnlyPublic { get; set; } = true;
        public bool OnlyOfficial { get; set; } = false;
        public List<string>? Tags { get; set; }
        public string? FileType { get; set; }
    }
}
