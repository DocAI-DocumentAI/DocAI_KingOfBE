namespace ChatBox.API.Payload.Request.ChatService
{
    public class AnalyticsRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string MetricType { get; set; } = "overview";
        public string TimeGranularity { get; set; } = "daily";
    }
}
