using AI.Domain.Models;

namespace AI.API.Payload.Response
{
    public class AIResponse : BaseResponse
    {
        public string Content { get; set; }
        public int TokensUsed { get; set; }
        public int ResponseTimeMs { get; set; }
        public string Text => Content;
    }

    public class StreamChunk
    {
        public string Content { get; set; }
        public bool IsComplete { get; set; }
        public int? TokenCount { get; set; }
        public string RequestId { get; set; }
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

    /// <summary>
    /// Aggregated metrics response
    /// </summary>
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

    /// <summary>
    /// Model-specific metrics
    /// </summary>
    public class ModelMetrics
    {
        public int RequestCount { get; set; }
        public long TokensUsed { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double SuccessRate { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    /// <summary>
    /// Service-specific metrics
    /// </summary>
    public class ServiceMetrics
    {
        public int RequestCount { get; set; }
        public long TokensUsed { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// Usage metric response
    /// </summary>
    public class UsageMetricResponse
    {
        public int Id { get; set; }
        public string RequestId { get; set; }
        public string SourceService { get; set; }
        public string ModelType { get; set; }
        public int TokensUsed { get; set; }
        public int ResponseTimeMs { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal? EstimatedCost { get; set; }
    }

    /// <summary>
    /// AI request log response
    /// </summary>
    public class AIRequestLogResponse
    {
        public int Id { get; set; }
        public string RequestId { get; set; }
        public string UserId { get; set; }
        public string SourceService { get; set; }
        public string ModelType { get; set; }
        public string PromptPreview { get; set; }
        public string ResponsePreview { get; set; }
        public int TokensUsed { get; set; }
        public int ResponseTimeMs { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public string RequestContent { get; set; }
        public string ResponseContent { get; set; }
        public decimal? EstimatedCost { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// Cleanup operation response
    /// </summary>
    public class CleanupResponse : BaseResponse
    {
        public int DaysToKeep { get; set; }
        public int RecordsDeleted { get; set; }
        public string InitiatedBy { get; set; }
        public DateTime InitiatedAt { get; set; }
    }

    public class PagedResponse<T> : BaseResponse
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int Size { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
}
