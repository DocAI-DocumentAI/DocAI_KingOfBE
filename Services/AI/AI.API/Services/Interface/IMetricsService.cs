using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.Domain.Models;
using AI.Infrastructure.FIlter;
using Auth.Infrastructure.Paginate;

namespace AI.API.Services.Interface
{
    public interface IMetricsService
    {
        Task LogUsageAsync(UsageMetric metric);
        Task<SystemMetrics> GetSystemMetricsAsync(DateTime? from = null, DateTime? to = null);
        Task<List<UsageMetric>> GetUsageHistoryAsync(string sourceService = null, DateTime? from = null, DateTime? to = null);
        Task<bool> CleanupOldMetricsAsync(int retentionDays = 90);

        // New methods for enhanced controller functionality
        Task<PagedResponse<UsageMetricResponse>> GetUsageMetricsAsync(GetUsageMetricsRequest request);
        Task<PagedResponse<AIRequestLogResponse>> GetRequestLogsAsync(GetLogsRequest request);
        Task<AggregatedMetricsResponse> GetAggregatedMetricsAsync(DateTime? from = null, DateTime? to = null);
        Task<Dictionary<string, object>> GetUserMetricsAsync(string userId, DateTime? from = null, DateTime? to = null);

        // Pagination Support
        Task<IPaginate<UsageMetric>> GetUsageMetricsPaginatedAsync(
            UsageMetricFilter filter = null,
            int page = 1,
            int size = 20,
            string sortBy = "CreatedAt",
            bool isAsc = false);

        // Advanced Analytics
        Task<Dictionary<string, object>> GetDetailedMetricsAsync(DateTime? from = null, DateTime? to = null);
    }
}
