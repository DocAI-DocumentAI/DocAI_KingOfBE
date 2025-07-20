using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.Domain.Models;

namespace AI.API.Services.Interface
{
    public interface IMetricsService
    {
        Task LogUsageAsync(UsageMetric metric);
        Task LogRequestAsync(AIRequestLog requestLog);
        Task<PagedResponse<UsageMetricResponse>> GetUsageMetricsAsync(GetMetricsRequest request);
        Task<PagedResponse<AIRequestLogResponse>> GetRequestLogsAsync(GetLogsRequest request);
        Task<AggregatedMetricsResponse> GetAggregatedMetricsAsync(DateTime? from, DateTime? to);
        Task<Dictionary<string, object>> GetUserMetricsAsync(string userId, DateTime? from, DateTime? to);
        Task<bool> CleanupOldMetricsAsync(int daysToKeep);
    }
}
