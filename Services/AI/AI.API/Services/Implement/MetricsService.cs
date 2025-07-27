
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.Domain.Enums;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Interfaces;
using Auth.Infrastructure.Paginate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using AI.Infrastructure.FIlter;
using AutoMapper;

namespace AI.API.Services.Implement
{
    /// <summary>
    /// Service responsible for tracking and retrieving AI usage metrics and request logs
    /// </summary>
    public class MetricsService : IMetricsService
    {
        private readonly IUnitOfWork<DocAIDbContext> _unitOfWork;
        private readonly ICacheService _cacheService;
        private readonly IAIConfigurationService _configService;
        private readonly ILogger<MetricsService> _logger;
        private readonly IMapper _mapper;
        private const string METRICS_CACHE_PREFIX = "metrics:";

        public MetricsService(
            IUnitOfWork<DocAIDbContext> unitOfWork,
            ICacheService cacheService,
            IAIConfigurationService configService,
            ILogger<MetricsService> logger,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task LogUsageAsync(UsageMetric metric)
        {
            if (metric == null)
                throw new ArgumentNullException(nameof(metric));

            try
            {
                // Check if metrics are enabled
                var metricsEnabled = await _configService.GetConfigurationAsync("AI:EnableMetrics", true);
                if (!metricsEnabled)
                {
                    _logger.LogDebug("Metrics collection is disabled, skipping log for request {RequestId}", metric.RequestId);
                    return;
                }

                // Calculate estimated cost
                if (metric.TokensUsed > 0)
                {
                    var costPerToken = await _configService.GetConfigurationAsync("AI:CostPerToken", 0.0001m);
                    metric.EstimatedCost = (decimal)metric.TokensUsed * costPerToken;
                }

                var repo = _unitOfWork.GetRepository<UsageMetric>();
                await repo.InsertAsync(metric);
                await _unitOfWork.CommitAsync();

                // Invalidate metrics cache
                await _cacheService.RemoveByPrefixAsync(METRICS_CACHE_PREFIX);

                _logger.LogDebug("Usage metric logged for request {RequestId}: {Tokens} tokens, {ResponseTime}ms, Status: {Status}",
                    metric.RequestId, metric.TokensUsed, metric.ResponseTimeMs, metric.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging usage metric for request {RequestId}", metric.RequestId);
                // Don't throw - metrics logging shouldn't break the main flow
            }
        }

        public async Task<SystemMetrics> GetSystemMetricsAsync(DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddDays(-1);
                var toDate = to ?? DateTime.UtcNow;

                var cacheKey = $"{METRICS_CACHE_PREFIX}system:{fromDate:yyyyMMddHH}:{toDate:yyyyMMddHH}";
                var cached = await _cacheService.GetAsync<SystemMetrics>(cacheKey);
                if (cached != null)
                {
                    _logger.LogDebug("System metrics cache hit for period {From} - {To}", fromDate, toDate);
                    return cached;
                }

                var repo = _unitOfWork.GetRepository<UsageMetric>();
                var metrics = await repo.GetListAsync(
                    predicate: m => m.CreatedAt >= fromDate && m.CreatedAt <= toDate);

                var systemMetrics = new SystemMetrics
                {
                    RequestsLast24Hours = metrics.Count,
                    SuccessfulRequests = metrics.Count(m => m.Status == RequestStatus.Completed),
                    FailedRequests = metrics.Count(m => m.Status == RequestStatus.Failed),
                    AverageResponseTime = metrics.Any() ? metrics.Average(m => m.ResponseTimeMs) : 0,
                    TotalTokensUsed = metrics.Sum(m => m.TokensUsed)
                };

                // Cache for 5 minutes
                await _cacheService.SetAsync(cacheKey, systemMetrics, TimeSpan.FromMinutes(5));

                _logger.LogInformation("System metrics calculated for period {From} - {To}: {Requests} requests, {Success} successful, {Failed} failed",
                    fromDate, toDate, systemMetrics.RequestsLast24Hours, systemMetrics.SuccessfulRequests, systemMetrics.FailedRequests);

                return systemMetrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system metrics for period {From} - {To}", from, to);
                return new SystemMetrics(); // Return empty metrics instead of throwing
            }
        }

        public async Task<List<UsageMetric>> GetUsageHistoryAsync(string sourceService = null, DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
                var toDate = to ?? DateTime.UtcNow;

                _logger.LogInformation("Getting usage history for service '{Service}' from {From} to {To}", sourceService, fromDate, toDate);

                var repo = _unitOfWork.GetRepository<UsageMetric>();
                var metrics = await repo.GetListAsync(
                    predicate: m =>
                        m.CreatedAt >= fromDate &&
                        m.CreatedAt <= toDate &&
                        (string.IsNullOrEmpty(sourceService) || m.SourceService == sourceService),
                    orderBy: q => q.OrderByDescending(m => m.CreatedAt));

                _logger.LogInformation("Retrieved {Count} usage metrics for service '{Service}'", metrics.Count, sourceService);
                return metrics.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage history for service {Service}", sourceService);
                return new List<UsageMetric>();
            }
        }

        public async Task<bool> CleanupOldMetricsAsync(int retentionDays = 90)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                _logger.LogInformation("Starting cleanup of metrics older than {CutoffDate} (retention: {RetentionDays} days)",
                    cutoffDate, retentionDays);

                var repo = _unitOfWork.GetRepository<UsageMetric>();

                // Delete in batches to avoid large transactions
                var batchSize = await _configService.GetConfigurationAsync("AI:CleanupBatchSize", 1000);
                var totalDeleted = 0;

                while (true)
                {
                    var batch = await repo.GetListAsync(
                        predicate: m => m.CreatedAt < cutoffDate,
                        orderBy: q => q.OrderBy(m => m.CreatedAt));

                    var batchToDelete = batch.Take(batchSize).ToList();

                    if (!batchToDelete.Any())
                        break;

                    repo.DeleteRangeAsync(batchToDelete);
                    await _unitOfWork.CommitAsync();

                    totalDeleted += batchToDelete.Count;
                    _logger.LogDebug("Deleted batch of {BatchSize} old metrics (total: {TotalDeleted})",
                        batchToDelete.Count, totalDeleted);

                    // Small delay to avoid overwhelming the database
                    await Task.Delay(100);
                }

                // Clear metrics cache after cleanup
                await _cacheService.RemoveByPrefixAsync(METRICS_CACHE_PREFIX);

                _logger.LogInformation("Metrics cleanup completed. Deleted {TotalDeleted} old metrics", totalDeleted);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metrics cleanup");
                return false;
            }
        }

        /// <summary>
        /// Get paginated usage metrics with filtering
        /// </summary>
        public async Task<IPaginate<UsageMetric>> GetUsageMetricsPaginatedAsync(
            UsageMetricFilter filter = null,
            int page = 1,
            int size = 20,
            string sortBy = "CreatedAt",
            bool isAsc = false)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<UsageMetric>();

                var result = await repo.GetPagingListAsync(
                    selector: m => m, // Select the full entity
                    filter: filter,
                    page: page,
                    size: size,
                    sortBy: sortBy,
                    isAsc: isAsc
                );

                _logger.LogDebug("Retrieved paginated usage metrics: Page {Page}, Size {Size}, Total {Total}",
                    page, size, result.Total);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated usage metrics");
                throw;
            }
        }

        /// <summary>
        /// Get detailed metrics breakdown by model type with pagination
        /// </summary>
        public async Task<Dictionary<string, object>> GetDetailedMetricsAsync(DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddDays(-1);
                var toDate = to ?? DateTime.UtcNow;

                var cacheKey = $"{METRICS_CACHE_PREFIX}detailed:{fromDate:yyyyMMddHH}:{toDate:yyyyMMddHH}";
                var cached = await _cacheService.GetAsync<Dictionary<string, object>>(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                var repo = _unitOfWork.GetRepository<UsageMetric>();
                var metrics = await repo.GetListAsync(
                    predicate: m => m.CreatedAt >= fromDate && m.CreatedAt <= toDate);

                var result = new Dictionary<string, object>
                {
                    ["period"] = new { from = fromDate, to = toDate },
                    ["total_requests"] = metrics.Count,
                    ["by_model_type"] = metrics.GroupBy(m => m.ModelType)
                        .ToDictionary(g => g.Key.ToString(), g => new
                        {
                            requests = g.Count(),
                            tokens_used = g.Sum(m => m.TokensUsed),
                            avg_response_time = g.Average(m => m.ResponseTimeMs),
                            success_rate = g.Count(m => m.Status == RequestStatus.Completed) * 100.0 / g.Count(),
                            estimated_cost = g.Sum(m => m.EstimatedCost ?? 0)
                        }),
                    ["by_source_service"] = metrics.GroupBy(m => m.SourceService)
                        .ToDictionary(g => g.Key ?? "Unknown", g => new
                        {
                            requests = g.Count(),
                            tokens_used = g.Sum(m => m.TokensUsed),
                            avg_response_time = g.Average(m => m.ResponseTimeMs),
                            success_rate = g.Count(m => m.Status == RequestStatus.Completed) * 100.0 / g.Count()
                        }),
                    ["by_status"] = metrics.GroupBy(m => m.Status)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    ["hourly_distribution"] = metrics.GroupBy(m => m.CreatedAt.Hour)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ["performance"] = new
                    {
                        avg_response_time = metrics.Any() ? metrics.Average(m => m.ResponseTimeMs) : 0,
                        min_response_time = metrics.Any() ? metrics.Min(m => m.ResponseTimeMs) : 0,
                        max_response_time = metrics.Any() ? metrics.Max(m => m.ResponseTimeMs) : 0,
                        p95_response_time = metrics.Any() ?
                            metrics.OrderBy(m => m.ResponseTimeMs).Skip((int)(metrics.Count * 0.95)).FirstOrDefault()?.ResponseTimeMs ?? 0 : 0
                    },
                    ["costs"] = new
                    {
                        total_estimated_cost = metrics.Sum(m => m.EstimatedCost ?? 0),
                        avg_cost_per_request = metrics.Any() ?
                            metrics.Where(m => m.EstimatedCost.HasValue).Average(m => m.EstimatedCost.Value) : 0
                    }
                };

                // Cache for 10 minutes
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));

                _logger.LogDebug("Detailed metrics calculated for period {From} - {To}", fromDate, toDate);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed metrics");
                return new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["period"] = new { from, to }
                };
            }
        }

        // New methods for enhanced controller functionality
        public async Task<IPaginate<UsageMetricResponse>> GetUsageMetricsAsync(GetUsageMetricsRequest request)
        {
            try
            {
                var filter = new UsageMetricFilter
                {
                    SourceService = request.SourceService,
                    RequestId = request.UserId
                };

                var result = await GetUsageMetricsPaginatedAsync(
                    filter, request.Page, request.Size, request.SortBy, request.IsAscending
                );

                return new Paginate<UsageMetricResponse>
                {
                    Items = _mapper.Map<List<UsageMetricResponse>>(result.Items),
                    Page = request.Page,
                    Size = request.Size,
                    Total = result.Total
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage metrics");

                return new Paginate<UsageMetricResponse>
                {
                    Items = new List<UsageMetricResponse>(),
                    Page = request.Page,
                    Size = request.Size,
                    Total = 0
                };
            }
        }


        public async Task<IPaginate<AIRequestLogResponse>> GetRequestLogsAsync(GetLogsRequest request)
        {
            try
            {
                return new Paginate<AIRequestLogResponse>
                {
                    Page = request.Page,
                    Size = request.Size,
                    Total = 0,
                    Items = new List<AIRequestLogResponse>(),
                };
      
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs");
                return new Paginate<AIRequestLogResponse>
                {
                    Page = request.Page,
                    Size = request.Size,
                    Total = 0,
                    TotalPages = 0,
                    Items = new List<AIRequestLogResponse>()
                };
            }
        }

        public async Task<AggregatedMetricsResponse> GetAggregatedMetricsAsync(DateTime? from = null, DateTime? to = null)
        {
            try
            {
                var metrics = await GetSystemMetricsAsync(from, to);

                return new AggregatedMetricsResponse
                {
                    Success = true,
                    TotalRequests = metrics.TotalRequests,
                    SuccessfulRequests = metrics.SuccessfulRequests,
                    FailedRequests = metrics.FailedRequests,
                    SuccessRate = metrics.SuccessRate,
                    AverageResponseTimeMs = metrics.AverageResponseTimeMs,
                    TotalTokensUsed = metrics.TotalTokensUsed,
                    UniqueUsers = metrics.UniqueUsers,
                    FromDate = from ?? DateTime.UtcNow.AddDays(-1),
                    ToDate = to ?? DateTime.UtcNow,
                    MetricsByModel = new Dictionary<string, ModelMetrics>(),
                    MetricsByService = new Dictionary<string, ServiceMetrics>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting aggregated metrics");
                return new AggregatedMetricsResponse
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<Dictionary<string, object>> GetUserMetricsAsync(string userId, DateTime? from = null, DateTime? to = null)
        {
            try
            {
                // Placeholder implementation
                return new Dictionary<string, object>
                {
                    ["userId"] = userId,
                    ["totalRequests"] = 0,
                    ["totalTokens"] = 0,
                    ["averageResponseTime"] = 0.0,
                    ["from"] = from ?? DateTime.UtcNow.AddDays(-30),
                    ["to"] = to ?? DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user metrics for {UserId}", userId);
                return new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                };
            }
        }
    }
}
