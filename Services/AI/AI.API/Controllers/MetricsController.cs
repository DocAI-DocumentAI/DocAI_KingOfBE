using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace AI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController : ControllerBase
    {
        private readonly IMetricsService _metricsService;
        private readonly ILogger<MetricsController> _logger;

        public MetricsController(
            IMetricsService metricsService,
            ILogger<MetricsController> logger)
        {
            _metricsService = metricsService;
            _logger = logger;
        }

        /// <summary>
        /// Get usage metrics with pagination and filtering
        /// </summary>
        //[HttpGet("usage")]
        //public async Task<ActionResult<PagedResponse<UsageMetricResponse>>> GetUsageMetrics([FromQuery] GetUsageMetricsRequest request)
        //{
        //    try
        //    {
        //        var metrics = await _metricsService.GetUsageMetricsAsync(request);
        //        return Ok(metrics);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error getting usage metrics");
        //        return StatusCode(500, new { message = "Internal server error" });
        //    }
        //}

        ///// <summary>
        ///// Get request logs with pagination and filtering
        ///// </summary>
        //[HttpGet("logs")]
        //public async Task<ActionResult<PagedResponse<AIRequestLogResponse>>> GetRequestLogs([FromQuery] GetLogsRequest request)
        //{
        //    try
        //    {
        //        var logs = await _metricsService.GetRequestLogsAsync(request);
        //        return Ok(logs);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error getting request logs");
        //        return StatusCode(500, new { message = "Internal server error" });
        //    }
        //}

        /// <summary>
        /// Get aggregated metrics for a time period
        /// </summary>
        [HttpGet("aggregated")]
        public async Task<ActionResult<AggregatedMetricsResponse>> GetAggregatedMetrics(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                var metrics = await _metricsService.GetAggregatedMetricsAsync(from, to);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting aggregated metrics");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get system health metrics
        /// </summary>
        [HttpGet("health")]
        public async Task<ActionResult> GetHealthMetrics()
        {
            try
            {
                // Basic health check
                var healthData = new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    uptime = Environment.TickCount64,
                    memoryUsage = GC.GetTotalMemory(false)
                };

                return Ok(healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health metrics");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get metrics summary for dashboard
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult> GetMetricsSummary([FromQuery] int days = 7)
        {
            try
            {
                var from = DateTime.UtcNow.AddDays(-days);
                var to = DateTime.UtcNow;
                
                var aggregated = await _metricsService.GetAggregatedMetricsAsync(from, to);
                
                var summary = new
                {
                    period = new { from, to, days },
                    totalRequests = aggregated.TotalRequests,
                    successRate = aggregated.TotalRequests > 0 
                        ? (double)aggregated.SuccessfulRequests / aggregated.TotalRequests * 100 
                        : 0,
                    averageResponseTime = aggregated.AverageResponseTimeMs,
                    totalTokens = aggregated.TotalTokensUsed
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metrics summary");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
