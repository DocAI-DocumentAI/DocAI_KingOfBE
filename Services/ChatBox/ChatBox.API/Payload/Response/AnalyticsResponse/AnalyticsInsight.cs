namespace ChatBox.API.Payload.Response.AnalyticsResponse
{
    public class AnalyticsInsight
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }
}
