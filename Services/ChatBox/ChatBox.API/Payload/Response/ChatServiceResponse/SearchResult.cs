namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class SearchResult
    {
        public Guid MessageId { get; set; }
        public Guid SessionId { get; set; }
        public string Content { get; set; }
        public string Response { get; set; }
        public DateTime CreatedAt { get; set; }
        public double RelevanceScore { get; set; }
        public string MatchContext { get; set; }
    }
}
