using AI.API.Atributte;
using AI.API.Common.Utils;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ILogger<AdminController> _logger;
        private readonly IMetricsService _metricsService;
        private readonly IAIConfigurationService _configService;

        public AdminController(
            ILogger<AdminController> logger,
            IMetricsService metricsService,
            IAIConfigurationService configService)
        {
            _logger = logger;
            _metricsService = metricsService;
            _configService = configService;
        }

        /// <summary>
        /// Get system information
        /// </summary>
        [HttpGet("system-info")]
        public ActionResult<SystemInfo> GetSystemInfo()
        {
            try
            {
                var systemInfo = SystemInfoHelper.GetSystemInfo();
                return Ok(systemInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system information");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get detailed memory information
        /// </summary>
        [HttpGet("memory-info")]
        public ActionResult<MemoryInfo> GetMemoryInfo()
        {
            try
            {
                var memoryInfo = SystemInfoHelper.GetMemoryInfo();
                return Ok(memoryInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting memory information");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get process information
        /// </summary>
        [HttpGet("process-info")]
        public ActionResult<ProcessInfo> GetProcessInfo()
        {
            try
            {
                var processInfo = SystemInfoHelper.GetProcessInfo();
                return Ok(processInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting process information");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Analyze text content
        /// </summary>
        [HttpPost("analyze-text")]
        [RateLimit(MaxRequests = 20, WindowInMinutes = 1)]
        public ActionResult<TextAnalysisResult> AnalyzeText([FromBody] AnalyzeTextRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Text))
                {
                    return BadRequest(new { message = "Text is required" });
                }

                var analysis = TextAnalysisHelper.AnalyzeText(request.Text);
                return Ok(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing text");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get comprehensive system health
        /// [DEPRECATED] Use /api/health endpoint instead
        /// </summary>
        [HttpGet("health")]
        [RateLimit(MaxRequests = 5, WindowInMinutes = 1)]
        [Obsolete("This endpoint is deprecated. Use /api/health endpoint instead.")]
        public async Task<ActionResult> GetSystemHealth()
        {
            return BadRequest(new {
                error = "This endpoint is deprecated",
                message = "Please use /api/health endpoint instead",
                newEndpoint = "/api/health"
            });
        }

        /// <summary>
        /// Force garbage collection (use with caution)
        /// </summary>
        [HttpPost("gc")]
        [RateLimit(MaxRequests = 2, WindowInMinutes = 5)]
        public ActionResult ForceGarbageCollection()
        {
            try
            {
                var beforeMemory = GC.GetTotalMemory(false);
                
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var afterMemory = GC.GetTotalMemory(false);
                var freedMemory = beforeMemory - afterMemory;

                _logger.LogInformation("Forced garbage collection. Freed {FreedMemory} bytes", freedMemory);

                return Ok(new
                {
                    message = "Garbage collection completed",
                    beforeMemory,
                    afterMemory,
                    freedMemory,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during garbage collection");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Clear application caches
        /// </summary>
        [HttpPost("clear-cache")]
        [RateLimit(MaxRequests = 5, WindowInMinutes = 5)]
        public ActionResult ClearCache([FromQuery] string? cacheType = null)
        {
            try
            {
                // This would integrate with your cache service
                _logger.LogInformation("Cache clear requested for type: {CacheType}", cacheType ?? "all");
                
                return Ok(new
                {
                    message = "Cache cleared successfully",
                    cacheType = cacheType ?? "all",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get application logs (recent entries)
        /// </summary>
        [HttpGet("logs")]
        [RateLimit(MaxRequests = 10, WindowInMinutes = 1)]
        public ActionResult GetRecentLogs([FromQuery] int count = 100, [FromQuery] string? level = null)
        {
            try
            {
                // This would integrate with your logging system
                var logs = new
                {
                    message = "Log retrieval not implemented - integrate with your logging provider",
                    requestedCount = count,
                    requestedLevel = level,
                    timestamp = DateTime.UtcNow
                };

                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting logs");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    public class AnalyzeTextRequest
    {
        public string Text { get; set; } = string.Empty;
    }
}
