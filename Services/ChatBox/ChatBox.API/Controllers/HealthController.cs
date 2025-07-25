using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Services.Interfaces;
using ChatBox.API.Payload.Response.HealthMonitoringResponses;
using ChatBox.API.Payload.Response.ChatServiceResponse;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IServiceHealthService _healthService;
        private readonly ILogger<HealthController> _logger;

        public HealthController(IServiceHealthService healthService, ILogger<HealthController> logger)
        {
            _healthService = healthService;
            _logger = logger;
        }

        /// <summary>
        /// Get system status (public endpoint for health checks)
        /// </summary>
        [HttpGet("status")]
        [AllowAnonymous]
        public async Task<ActionResult<SystemStatusResponse>> GetSystemStatus()
        {
            try
            {
                var response = await _healthService.GetSystemStatusAsync();
                
                // Return appropriate HTTP status based on system health
                if (response.OverallStatus == "Healthy")
                {
                    return Ok(response);
                }
                else if (response.OverallStatus == "Degraded")
                {
                    return StatusCode(207, response); // Multi-Status
                }
                else
                {
                    return StatusCode(503, response); // Service Unavailable
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system status");
                return StatusCode(500, new { 
                    message = "Internal server error", 
                    status = "Unhealthy",
                    timestamp = DateTime.UtcNow 
                });
            }
        }

        /// <summary>
        /// Get active alerts (requires authentication)
        /// </summary>
        [HttpGet("alerts")]
        [Authorize]
        public async Task<ActionResult<List<AlertResponse>>> GetActiveAlerts()
        {
            try
            {
                var alerts = await _healthService.GetActiveAlertsAsync();
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active alerts");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get performance metrics (Admin only)
        /// </summary>
        [HttpGet("metrics")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PerformanceMetrics>> GetPerformanceMetrics()
        {
            try
            {
                var metrics = await _healthService.GetPerformanceMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance metrics");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Simple ping endpoint for basic health check
        /// </summary>
        [HttpGet("ping")]
        [AllowAnonymous]
        public ActionResult<object> Ping()
        {
            return Ok(new { 
                status = "OK", 
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }

        /// <summary>
        /// Readiness probe for Kubernetes
        /// </summary>
        [HttpGet("ready")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> Ready()
        {
            try
            {
                var systemStatus = await _healthService.GetSystemStatusAsync();
                
                if (systemStatus.OverallStatus == "Healthy" || systemStatus.OverallStatus == "Degraded")
                {
                    return Ok(new { 
                        status = "Ready", 
                        timestamp = DateTime.UtcNow 
                    });
                }
                else
                {
                    return StatusCode(503, new { 
                        status = "Not Ready", 
                        timestamp = DateTime.UtcNow 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking readiness");
                return StatusCode(503, new { 
                    status = "Not Ready", 
                    timestamp = DateTime.UtcNow,
                    error = "Health check failed"
                });
            }
        }

        /// <summary>
        /// Liveness probe for Kubernetes
        /// </summary>
        [HttpGet("live")]
        [AllowAnonymous]
        public ActionResult<object> Live()
        {
            return Ok(new { 
                status = "Alive", 
                timestamp = DateTime.UtcNow 
            });
        }
    }
}
