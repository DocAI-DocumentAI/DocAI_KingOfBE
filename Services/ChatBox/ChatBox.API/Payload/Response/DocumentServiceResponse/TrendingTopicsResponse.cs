namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class TrendingTopicsResponse
    {
        public List<TrendingTopic> Topics { get; set; } = new();
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string TimeGranularity { get; set; }
    }
}
