using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Services.Interfaces;
using ChatBox.API.Payload.Response.SecurityServiceResponse;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class SecurityController : ControllerBase
    {
        private readonly ISecurityService _securityService;
        private readonly ILogger<SecurityController> _logger;

        public SecurityController(ISecurityService securityService, ILogger<SecurityController> logger)
        {
            _securityService = securityService;
            _logger = logger;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private string GetIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        /// <summary>
        /// Analyze content for security threats
        /// </summary>
        [HttpPost("analyze")]
        public async Task<ActionResult<SecurityAnalysisResult>> AnalyzeContent([FromBody] SecurityAnalysisRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Content))
                {
                    return BadRequest(new { message = "Content is required" });
                }

                var userId = GetUserId();
                var ipAddress = GetIpAddress();

                var analysis = await _securityService.AnalyzeContentAsync(request.Content, userId, ipAddress);
                return Ok(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing content for security threats");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Detect PII (Personally Identifiable Information) in content
        /// </summary>
        [HttpPost("detect-pii")]
        public async Task<ActionResult<PIIDetectionResult>> DetectPII([FromBody] PIIDetectionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Content))
                {
                    return BadRequest(new { message = "Content is required" });
                }

                var result = await _securityService.DetectPIIAsync(request.Content);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting PII in content");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get security events for current user
        /// </summary>
        [HttpGet("events")]
        public async Task<ActionResult<List<SecurityEvent>>> GetSecurityEvents([FromQuery] DateTime? fromDate)
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { message = "Invalid user ID" });
                }

                var events = await _securityService.GetSecurityEventsAsync(userId, fromDate);
                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security events for user");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get security events for specific user (Admin only)
        /// </summary>
        [HttpGet("events/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<SecurityEvent>>> GetUserSecurityEvents(Guid userId, [FromQuery] DateTime? fromDate)
        {
            try
            {
                var events = await _securityService.GetSecurityEventsAsync(userId, fromDate);
                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security events for user {UserId}", userId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }


    }

    // Helper request/response classes
    public class SecurityAnalysisRequest
    {
        public string Content { get; set; } = string.Empty;
        public string? Context { get; set; }
    }

    public class PIIDetectionRequest
    {
        public string Content { get; set; } = string.Empty;
        public bool MaskPII { get; set; } = false;
    }

    public class ThreatStatusUpdate
    {
        public string Status { get; set; } = string.Empty; // Active, Resolved, Investigating
        public string? Resolution { get; set; }
    }

    public class SecurityReportRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<string> IncludeMetrics { get; set; } = new();
        public string Format { get; set; } = "json"; // json, pdf, csv
    }

    public class SecurityDashboard
    {
        public int TotalThreats { get; set; }
        public int ActiveThreats { get; set; }
        public int ResolvedThreats { get; set; }
        public int PIIDetections { get; set; }
        public int SecurityEvents { get; set; }
        public List<SecurityThreat> RecentThreats { get; set; } = new();
        public Dictionary<string, int> ThreatsByType { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class SecurityMetrics
    {
        public int TotalAnalyses { get; set; }
        public int ThreatsDetected { get; set; }
        public int PIIDetections { get; set; }
        public double ThreatDetectionRate { get; set; }
        public double FalsePositiveRate { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public Dictionary<string, int> MetricsByCategory { get; set; } = new();
    }

    public class SecurityReport
    {
        public string ReportId { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public SecurityMetrics Metrics { get; set; } = new();
        public List<SecurityThreat> Threats { get; set; } = new();
        public List<SecurityEvent> Events { get; set; } = new();
        public string Format { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }
}
