namespace AI.API.Payload.Response
{
    public class UsageMetricResponse
    {
        public string RequestId { get; set; }
        public string UserId { get; set; }
        public string ModelType { get; set; }
        public int TokensUsed { get; set; }
        public int ResponseTimeMs { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class AggregatedMetricsResponse : BaseResponse
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public long TotalTokensUsed { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public int UniqueUsers { get; set; }
        public Dictionary<string, ModelMetrics> MetricsByModel { get; set; }
        public Dictionary<int, int> HourlyDistribution { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public Dictionary<string, object> Metadata { get; set; }

    }
    public class ModelMetrics
    {
        public int RequestCount { get; set; }
        public long TokensUsed { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double SuccessRate {  get; set; }
    }
}
