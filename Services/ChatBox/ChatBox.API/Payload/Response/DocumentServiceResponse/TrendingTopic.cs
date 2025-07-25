namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class TrendingTopic
    {
        public string Topic { get; set; }
        public int SearchCount { get; set; }
        public double TrendScore { get; set; }
        public List<string> RelatedDocuments { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
