using System.Text.Json;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.Domain.Enums;
using AI.Domain.Models;
using AI.Infrastructure.FIlter;
using AI.Infrastructure.Repository.Interfaces;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace AI.API.Services.Implement
{
    public class MetricsService : IMetricsService
    {
        private readonly IUnitOfWork<DocAIDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IConfigurationService _configService;
        private readonly ILogger<MetricsService> _logger;
        private readonly SemaphoreSlim _logSemaphore = new(1, 1);
        public MetricsService(
            IUnitOfWork<DocAIDbContext> unitOfWork,
            IMapper mapper,
            IConfigurationService configService,
            ILogger<MetricsService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                    _logger.LogDebug("Metrics logging is disabled");
                    return;
                }

                // Use semaphore to prevent concurrent issues
                await _logSemaphore.WaitAsync();
                try
                {
                    // Validate metric data
                    if (string.IsNullOrEmpty(metric.RequestId))
                        metric.RequestId = Guid.NewGuid().ToString();

                    if (string.IsNullOrEmpty(metric.UserId))
                        metric.UserId = "anonymous";

                    if (metric.CreatedAt == default)
                        metric.CreatedAt = DateTime.UtcNow;

                    var repo = _unitOfWork.GetRepository<UsageMetric>();
                    await repo.InsertAsync(metric);
                    await _unitOfWork.CommitAsync();

                    _logger.LogDebug("Usage metric logged: {RequestId} - {ModelType} - {Tokens}ms - {Status}",
                        metric.RequestId, metric.ModelType, metric.TokensUsed, metric.Status);

                    // Check if cleanup is needed
                    await CheckAndTriggerCleanupAsync();
                }
                finally
                {
                    _logSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                // Don't throw - metrics logging should not break the main flow
                _logger.LogError(ex, "Failed to log usage metric for request {RequestId}", metric.RequestId);
            }
        }

        public async Task LogRequestAsync(AIRequestLog requestLog)
        {
            if (requestLog == null)
                throw new ArgumentNullException(nameof(requestLog));

            try
            {
                // Check if request logging is enabled
                var loggingEnabled = await _configService.GetConfigurationAsync("AI:EnableRequestLogging", true);
                if (!loggingEnabled)
                {
                    _logger.LogDebug("Request logging is disabled");
                    return;
                }

                await _logSemaphore.WaitAsync();
                try
                {
                    // Validate and sanitize
                    if (string.IsNullOrEmpty(requestLog.RequestId))
                        requestLog.RequestId = Guid.NewGuid().ToString();

                    if (requestLog.CreatedAt == default)
                        requestLog.CreatedAt = DateTime.UtcNow;

                    // Truncate large content if needed
                    var maxContentLength = await _configService.GetConfigurationAsync("AI:MaxLogContentLength", 5000);

                    if (!string.IsNullOrEmpty(requestLog.RequestContent) && requestLog.RequestContent.Length > maxContentLength)
                    {
                        requestLog.RequestContent = requestLog.RequestContent.Substring(0, maxContentLength) + "...";
                    }

                    if (!string.IsNullOrEmpty(requestLog.ResponseContent) && requestLog.ResponseContent.Length > maxContentLength)
                    {
                        requestLog.ResponseContent = requestLog.ResponseContent.Substring(0, maxContentLength) + "...";
                    }

                    var repo = _unitOfWork.GetRepository<AIRequestLog>();
                    await repo.InsertAsync(requestLog);
                    await _unitOfWork.CommitAsync();

                    _logger.LogDebug("Request logged: {RequestId} - {Status}", requestLog.RequestId, requestLog.Status);
                }
                finally
                {
                    _logSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                // Don't throw - logging should not break the main flow
                _logger.LogError(ex, "Failed to log request {RequestId}", requestLog.RequestId);
            }
        }

        public async Task<PagedResponse<UsageMetricResponse>> GetUsageMetricsAsync(GetMetricsRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                // Validate pagination
                if (request.PageNumber < 1) request.PageNumber = 1;
                if (request.PageSize < 1) request.PageSize = 20;
                if (request.PageSize > 100) request.PageSize = 100; // Max page size

                var filter = new UsageMetricFilter
                {
                    UserId = request.UserId,
                    ModelType = !string.IsNullOrEmpty(request.ModelType) && Enum.TryParse<ModelType>(request.ModelType, true, out var type)
                        ? type
                        : null,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate
                };

                var repo = _unitOfWork.GetRepository<UsageMetric>();
                var pagedResult = await repo.GetPagingListAsync(
                    selector: m => _mapper.Map<UsageMetricResponse>(m),
                    filter: filter,
                    orderBy: q => q.OrderByDescending(m => m.CreatedAt),
                    page: request.PageNumber,
                    size: request.PageSize
                );

                return new PagedResponse<UsageMetricResponse>
                {
                    Success = true,
                    Items = pagedResult.Items.ToList(),
                    PageNumber = pagedResult.Page,
                    PageSize = pagedResult.Size,
                    TotalPages = pagedResult.TotalPages,
                    TotalCount = pagedResult.Total,
                    HasPrevious = pagedResult.Page > 1,
                    HasNext = pagedResult.Page < pagedResult.TotalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage metrics");
                throw;
            }
        }

        public async Task<PagedResponse<AIRequestLogResponse>> GetRequestLogsAsync(GetLogsRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                // Validate pagination
                if (request.PageNumber < 1) request.PageNumber = 1;
                if (request.PageSize < 1) request.PageSize = 20;
                if (request.PageSize > 100) request.PageSize = 100;

                var filter = new AIRequestLogFilter
                {
                    UserId = request.UserId,
                    RequestId = request.RequestId,
                    ModelType = !string.IsNullOrEmpty(request.ModelType) && Enum.TryParse<ModelType>(request.ModelType, true, out var type)
                        ? type
                        : null,
                    Status = !string.IsNullOrEmpty(request.Status) && Enum.TryParse<RequestStatus>(request.Status, true, out var status)
                        ? status
                        : null,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate
                };

                var repo = _unitOfWork.GetRepository<AIRequestLog>();
                var pagedResult = await repo.GetPagingListAsync(
                    selector: log => _mapper.Map<AIRequestLogResponse>(log),
                    filter: filter,
                    orderBy: q => q.OrderByDescending(l => l.CreatedAt),
                    page: request.PageNumber,
                    size: request.PageSize
                );

                return new PagedResponse<AIRequestLogResponse>
                {
                    Success = true,
                    Items = pagedResult.Items.ToList(),
                    PageNumber = pagedResult.Page,
                    PageSize = pagedResult.Size,
                    TotalPages = pagedResult.TotalPages,
                    TotalCount = pagedResult.Total,
                    HasPrevious = pagedResult.Page > 1,
                    HasNext = pagedResult.Page < pagedResult.TotalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs");
                throw;
            }
        }

        public async Task<AggregatedMetricsResponse> GetAggregatedMetricsAsync(DateTime? from, DateTime? to)
        {
            try
            {
                // Default to last 7 days
                from ??= DateTime.UtcNow.AddDays(-7);
                to ??= DateTime.UtcNow;

                // Validate date range
                if (from > to)
                {
                    throw new ArgumentException("From date cannot be after To date");
                }

                var maxDays = await _configService.GetConfigurationAsync("AI:MaxMetricsDays", 90);
                if ((to.Value - from.Value).TotalDays > maxDays)
                {
                    throw new ArgumentException($"Date range cannot exceed {maxDays} days");
                }

                var repo = _unitOfWork.GetRepository<UsageMetric>();
                var metrics = await repo.GetListAsync(
                    predicate: m => m.CreatedAt >= from && m.CreatedAt <= to);

                if (!metrics.Any())
                {
                    return new AggregatedMetricsResponse
                    {
                        Success = true,
                        TotalRequests = 0,
                        SuccessfulRequests = 0,
                        FailedRequests = 0,
                        TotalTokensUsed = 0,
                        AverageResponseTimeMs = 0,
                        UniqueUsers = 0,
                        MetricsByModel = new Dictionary<string, ModelMetrics>(),
                        HourlyDistribution = new Dictionary<int, int>(),
                        FromDate = from.Value,
                        ToDate = to.Value
                    };
                }

                // Calculate aggregations
                var response = new AggregatedMetricsResponse
                {
                    Success = true,
                    TotalRequests = metrics.Count,
                    SuccessfulRequests = metrics.Count(m => m.Status == RequestStatus.Completed),
                    FailedRequests = metrics.Count(m => m.Status == RequestStatus.Failed),
                    TotalTokensUsed = metrics.Sum(m => (long)m.TokensUsed),
                    AverageResponseTimeMs = metrics.Average(m => m.ResponseTimeMs),
                    UniqueUsers = metrics.Select(m => m.UserId).Distinct().Count(),
                    FromDate = from.Value,
                    ToDate = to.Value
                };

                // Group by model type
                response.MetricsByModel = metrics
                    .GroupBy(m => m.ModelType)
                    .ToDictionary(
                        g => g.Key.ToString(),
                        g => new ModelMetrics
                        {
                            RequestCount = g.Count(),
                            TokensUsed = g.Sum(m => (long)m.TokensUsed),
                            AverageResponseTimeMs = g.Average(m => m.ResponseTimeMs),
                            SuccessCount = g.Count(m => m.Status == RequestStatus.Completed),
                            FailureCount = g.Count(m => m.Status == RequestStatus.Failed),
                            SuccessRate = g.Count() > 0
                                ? (double)g.Count(m => m.Status == RequestStatus.Completed) / g.Count() * 100
                                : 0
                        }
                    );

                // Hourly distribution
                response.HourlyDistribution = metrics
                    .GroupBy(m => m.CreatedAt.Hour)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Add daily statistics
                response.Metadata = new Dictionary<string, object>
                {
                    ["dailyStats"] = metrics
                        .GroupBy(m => m.CreatedAt.Date)
                        .OrderBy(g => g.Key)
                        .Select(g => new
                        {
                            date = g.Key.ToString("yyyy-MM-dd"),
                            requests = g.Count(),
                            tokens = g.Sum(m => m.TokensUsed),
                            avgResponseTime = g.Average(m => m.ResponseTimeMs),
                            successRate = g.Count() > 0
                                ? (double)g.Count(m => m.Status == RequestStatus.Completed) / g.Count() * 100
                                : 0
                        })
                        .ToList(),
                    ["peakHour"] = response.HourlyDistribution.Any()
                        ? response.HourlyDistribution.OrderByDescending(h => h.Value).First().Key
                        : 0,
                    ["avgTokensPerRequest"] = response.TotalRequests > 0
                        ? response.TotalTokensUsed / response.TotalRequests
                        : 0
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting aggregated metrics");
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetUserMetricsAsync(string userId, DateTime? from, DateTime? to)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be empty", nameof(userId));

            try
            {
                // Default to last 30 days
                from ??= DateTime.UtcNow.AddDays(-30);
                to ??= DateTime.UtcNow;

                var repo = _unitOfWork.GetRepository<UsageMetric>();
                var userMetrics = await repo.GetListAsync(
                    predicate: m => m.UserId == userId && m.CreatedAt >= from && m.CreatedAt <= to,
                    orderBy: q => q.OrderByDescending(m => m.CreatedAt));

                if (!userMetrics.Any())
                {
                    return new Dictionary<string, object>
                    {
                        ["userId"] = userId,
                        ["totalRequests"] = 0,
                        ["totalTokensUsed"] = 0,
                        ["successRate"] = 0,
                        ["fromDate"] = from.Value,
                        ["toDate"] = to.Value,
                        ["hasData"] = false
                    };
                }

                // Calculate user statistics
                var totalRequests = userMetrics.Count;
                var successfulRequests = userMetrics.Count(m => m.Status == RequestStatus.Completed);

                var stats = new Dictionary<string, object>
                {
                    ["userId"] = userId,
                    ["totalRequests"] = totalRequests,
                    ["successfulRequests"] = successfulRequests,
                    ["failedRequests"] = userMetrics.Count(m => m.Status == RequestStatus.Failed),
                    ["totalTokensUsed"] = userMetrics.Sum(m => m.TokensUsed),
                    ["averageTokensPerRequest"] = Math.Round(userMetrics.Average(m => m.TokensUsed), 2),
                    ["averageResponseTimeMs"] = Math.Round(userMetrics.Average(m => m.ResponseTimeMs), 2),
                    ["successRate"] = Math.Round((double)successfulRequests / totalRequests * 100, 2),
                    ["modelUsage"] = userMetrics
                        .GroupBy(m => m.ModelType)
                        .ToDictionary(
                            g => g.Key.ToString(),
                            g => new
                            {
                                count = g.Count(),
                                percentage = Math.Round((double)g.Count() / totalRequests * 100, 2),
                                totalTokens = g.Sum(m => m.TokensUsed),
                                avgResponseTime = Math.Round(g.Average(m => m.ResponseTimeMs), 2)
                            }
                        ),
                    ["recentRequests"] = userMetrics
                        .Take(10)
                        .Select(m => new
                        {
                            requestId = m.RequestId,
                            modelType = m.ModelType.ToString(),
                            tokensUsed = m.TokensUsed,
                            responseTimeMs = m.ResponseTimeMs,
                            status = m.Status.ToString(),
                            createdAt = m.CreatedAt,
                            error = m.ErrorMessage
                        })
                        .ToList(),
                    ["dailyUsage"] = userMetrics
                        .GroupBy(m => m.CreatedAt.Date)
                        .OrderBy(g => g.Key)
                        .Select(g => new
                        {
                            date = g.Key.ToString("yyyy-MM-dd"),
                            requests = g.Count(),
                            tokens = g.Sum(m => m.TokensUsed),
                            avgResponseTime = Math.Round(g.Average(m => m.ResponseTimeMs), 2),
                            successRate = Math.Round((double)g.Count(m => m.Status == RequestStatus.Completed) / g.Count() * 100, 2)
                        })
                        .ToList(),
                    ["peakUsageHour"] = userMetrics
                        .GroupBy(m => m.CreatedAt.Hour)
                        .OrderByDescending(g => g.Count())
                        .Select(g => new { hour = g.Key, count = g.Count() })
                        .FirstOrDefault(),
                    ["fromDate"] = from.Value,
                    ["toDate"] = to.Value,
                    ["hasData"] = true
                };

                // Add cost estimation if configured
                var costPerToken = await _configService.GetConfigurationAsync<decimal>("AI:CostPerToken", 0);
                if (costPerToken > 0)
                {
                    stats["estimatedCost"] = Math.Round(userMetrics.Sum(m => m.TokensUsed) * costPerToken, 4);
                    stats["costCurrency"] = await _configService.GetConfigurationAsync("AI:CostCurrency", "USD");
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user metrics for {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> CleanupOldMetricsAsync(int daysToKeep)
        {
            if (daysToKeep < 1)
                throw new ArgumentException("Days to keep must be at least 1", nameof(daysToKeep));

            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
                var deletedCount = 0;

                _logger.LogInformation("Starting metrics cleanup for data older than {CutoffDate}", cutoffDate);

                // Cleanup in batches to avoid locking
                var batchSize = await _configService.GetConfigurationAsync("AI:CleanupBatchSize", 1000);

                // Cleanup usage metrics
                var metricsRepo = _unitOfWork.GetRepository<UsageMetric>();
                while (true)
                {
                    var oldMetrics = await metricsRepo.GetListAsync(
                        predicate: m => m.CreatedAt < cutoffDate,
                        orderBy: q => q.OrderBy(m => m.CreatedAt));

                    if (!oldMetrics.Any())
                        break;

                    metricsRepo.DeleteRangeAsync(oldMetrics);
                    await _unitOfWork.CommitAsync();
                    deletedCount += oldMetrics.Count;

                    _logger.LogDebug("Deleted {Count} usage metrics", oldMetrics.Count);

                    // Small delay to prevent overload
                    await Task.Delay(100);
                }

                // Cleanup request logs
                var logsRepo = _unitOfWork.GetRepository<AIRequestLog>();
                while (true)
                {
                    var oldLogs = await logsRepo.GetListAsync(
                        predicate: l => l.CreatedAt < cutoffDate,
                        orderBy: q => q.OrderBy(l => l.CreatedAt));

                    if (!oldLogs.Any())
                        break;

                    logsRepo.DeleteRangeAsync(oldLogs);
                    await _unitOfWork.CommitAsync();
                    deletedCount += oldLogs.Count;

                    _logger.LogDebug("Deleted {Count} request logs", oldLogs.Count);

                    await Task.Delay(100);
                }

                _logger.LogInformation("Metrics cleanup completed. Deleted {TotalCount} records", deletedCount);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metrics cleanup");
                throw;
            }
        }
        #region Private Methods

        private async Task CheckAndTriggerCleanupAsync()
        {
            try
            {
                // Check if auto cleanup is enabled
                var autoCleanupEnabled = await _configService.GetConfigurationAsync("AI:EnableAutoCleanup", false);
                if (!autoCleanupEnabled)
                    return;

                // Check last cleanup time
                var lastCleanup = await _configService.GetConfigurationAsync<DateTime?>("AI:LastMetricsCleanup", null);
                var cleanupIntervalDays = await _configService.GetConfigurationAsync("AI:CleanupIntervalDays", 7);

                if (lastCleanup.HasValue && (DateTime.UtcNow - lastCleanup.Value).TotalDays < cleanupIntervalDays)
                    return;

                // Trigger cleanup in background
                var retentionDays = await _configService.GetConfigurationAsync("AI:MetricsRetentionDays", 90);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await CleanupOldMetricsAsync(retentionDays);
                        await _configService.SetConfigurationAsync(new UpdateConfigurationRequest
                        {
                            Key = "AI:LastMetricsCleanup",
                            Value = DateTime.UtcNow.ToString("O"),
                            Category = "System"
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background metrics cleanup failed");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cleanup trigger");
            }
        }

        #endregion
    }
}
