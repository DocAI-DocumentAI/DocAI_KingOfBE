using AI.Domain.Models;

namespace AI.API.Payload.Response
{
    public class AIResponse 
    {
        public bool Success { get; set; }
        public string RequestId { get; set; }
        public string Content { get; set; }
        public int TokensUsed { get; set; }
        public int ResponseTimeMs { get; set; }
        public string? ModelUsed { get; set; }
        public string? Message { get; set; }

        // Context-specific properties
        public int DocumentsUsed { get; set; }
        public int ConversationHistoryLength { get; set; }
        public string? DetectedIntent { get; set; }
        public double IntentConfidence { get; set; }
        public int ContextTokens { get; set; }
    }

    /// <summary>
    /// System metrics response
    /// </summary>
    public class SystemMetrics
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double SuccessRate { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public long TotalTokensUsed { get; set; }
        public int UniqueUsers { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int RequestsLast24Hours { get; set; }
        public double AverageResponseTime { get; set; }
        public DateTime LastUpdated { get; set; }
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }
    public class AggregatedMetricsResponse : BaseResponse
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double SuccessRate { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public long TotalTokensUsed { get; set; }
        public int UniqueUsers { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public Dictionary<string, ModelMetrics> MetricsByModel { get; set; } = new();
        public Dictionary<string, ServiceMetrics> MetricsByService { get; set; } = new();
    }

    public class ModelMetrics
    {
        public int RequestCount { get; set; }
        public long TokensUsed { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double SuccessRate { get; set; }
        public decimal EstimatedCost { get; set; }
    }
    public class ServiceMetrics
    {
        public int RequestCount { get; set; }
        public long TokensUsed { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double SuccessRate { get; set; }
    }

}
