using AI.API.Constants;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace AI.API.Controllers
{
    [Authorize]
    [Route(ApiEndPointConstant.API_PREFIX + "/metrics")]
    public class MetricsController : BaseApiController
    {
        private readonly IMetricsService _metricsService;
        private readonly ILogger<MetricsController> _logger;

        public MetricsController(
            IMetricsService metricsService,
            ILogger<MetricsController> logger)
        {
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get usage metrics
        /// </summary>
        [HttpGet("usage")]
        [ProducesResponseType(typeof(PagedResponse<UsageMetricResponse>), 200)]
        public async Task<IActionResult> GetUsageMetrics([FromQuery] GetMetricsRequest request)
        {
            try
            {
                // Regular users can only see their own metrics
                if (!User.IsInRole("Admin"))
                {
                    request.UserId = User.Identity?.Name;
                }

                var response = await _metricsService.GetUsageMetricsAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting usage metrics");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Get request logs (Admin only)
        /// </summary>
        [HttpGet("logs")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(PagedResponse<AIRequestLogResponse>), 200)]
        public async Task<IActionResult> GetRequestLogs([FromQuery] GetLogsRequest request)
        {
            try
            {
                var response = await _metricsService.GetRequestLogsAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request logs");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Get aggregated metrics (Admin only)
        /// </summary>
        [HttpGet("aggregated")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(AggregatedMetricsResponse), 200)]
        public async Task<IActionResult> GetAggregatedMetrics(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                var response = await _metricsService.GetAggregatedMetricsAsync(from, to);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting aggregated metrics");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Get user-specific metrics
        /// </summary>
        [HttpGet("user/{userId}")]
        [ProducesResponseType(typeof(Dictionary<string, object>), 200)]
        public async Task<IActionResult> GetUserMetrics(
            string userId,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                // Regular users can only see their own metrics
                if (!User.IsInRole("Admin") && userId != User.Identity?.Name)
                {
                    return Forbid("You can only view your own metrics");
                }

                var metrics = await _metricsService.GetUserMetricsAsync(userId, from, to);
                return Ok(metrics);
            }
            catch (ArgumentException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user metrics for {UserId}", userId);
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Get current user's metrics
        /// </summary>
        [HttpGet("my-metrics")]
        [ProducesResponseType(typeof(Dictionary<string, object>), 200)]
        public async Task<IActionResult> GetMyMetrics(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                var userId = User.Identity?.Name;
                if (string.IsNullOrEmpty(userId))
                {
                    return HandleBadRequest("User identity not found");
                }

                var metrics = await _metricsService.GetUserMetricsAsync(userId, from, to);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user metrics");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Cleanup old metrics (Admin only)
        /// </summary>
        [HttpPost("cleanup")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> CleanupOldMetrics([FromQuery][Required] int daysToKeep = 90)
        {
            try
            {
                if (daysToKeep < 7)
                {
                    return HandleBadRequest("Days to keep must be at least 7");
                }

                var success = await _metricsService.CleanupOldMetricsAsync(daysToKeep);

                _logger.LogInformation("Metrics cleanup initiated by {User} for data older than {Days} days",
                    User.Identity?.Name, daysToKeep);

                return Ok(new
                {
                    success,
                    message = success
                        ? $"Cleanup completed for data older than {daysToKeep} days"
                        : "Cleanup failed",
                    daysToKeep,
                    initiatedBy = User.Identity?.Name,
                    initiatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metrics cleanup");
                return HandleError(ex);
            }
        }

        /// <summary>
        /// Export metrics to CSV (Admin only)
        /// </summary>
        [HttpGet("export")]
        [Authorize(Roles = "Admin")]
        [Produces("text/csv")]
        public async Task<IActionResult> ExportMetrics(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string format = "csv")
        {
            try
            {
                from ??= DateTime.UtcNow.AddDays(-30);
                to ??= DateTime.UtcNow;

                var metrics = await _metricsService.GetAggregatedMetricsAsync(from, to);

                if (format.ToLower() == "csv")
                {
                    var csv = GenerateMetricsCsv(metrics);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                    return File(bytes, "text/csv", $"ai_metrics_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
                }
                else
                {
                    return Ok(metrics);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting metrics");
                return HandleError(ex);
            }
        }

        private string GenerateMetricsCsv(AggregatedMetricsResponse metrics)
        {
            var csv = new System.Text.StringBuilder();

            // Headers
            csv.AppendLine("Metric,Value");

            // Summary metrics
            csv.AppendLine($"Total Requests,{metrics.TotalRequests}");
            csv.AppendLine($"Successful Requests,{metrics.SuccessfulRequests}");
            csv.AppendLine($"Failed Requests,{metrics.FailedRequests}");
            csv.AppendLine($"Total Tokens Used,{metrics.TotalTokensUsed}");
            csv.AppendLine($"Average Response Time (ms),{metrics.AverageResponseTimeMs:F2}");
            csv.AppendLine($"Unique Users,{metrics.UniqueUsers}");
            csv.AppendLine($"From Date,{metrics.FromDate:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine($"To Date,{metrics.ToDate:yyyy-MM-dd HH:mm:ss}");

            // Model breakdown
            csv.AppendLine();
            csv.AppendLine("Model Type,Request Count,Tokens Used,Avg Response Time,Success Rate");
            foreach (var model in metrics.MetricsByModel)
            {
                csv.AppendLine($"{model.Key},{model.Value.RequestCount},{model.Value.TokensUsed},{model.Value.AverageResponseTimeMs:F2},{model.Value.SuccessRate:F2}%");
            }

            return csv.ToString();
        }
    }
}
