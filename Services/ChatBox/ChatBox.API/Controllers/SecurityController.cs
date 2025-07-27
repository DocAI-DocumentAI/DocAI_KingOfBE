using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using ChatBox.API.Services.Interfaces;
using ChatBox.API.Payload.Response.SecurityServiceResponse;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{
    /// <summary>
    /// Controller for security analysis and PII detection operations
    /// </summary>
    [ApiController]
    [Route("api/v1/security")]
    [Authorize]
    public class SecurityController : ControllerBase
    {
        private readonly ISecurityService _securityService;
        private readonly IAuditService _auditService;
        private readonly ILogger<SecurityController> _logger;

        public SecurityController(
            ISecurityService securityService,
            IAuditService auditService,
            ILogger<SecurityController> logger)
        {
            _securityService = securityService;
            _auditService = auditService;
            _logger = logger;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        /// <summary>
        /// Analyzes content for security issues
        /// </summary>
        [HttpPost("analyze")]
        [ProducesResponseType(typeof(SecurityAnalysisResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SecurityAnalysisResult>> AnalyzeContent(
            [FromBody] SecurityAnalysisRequest request)
        {
            try
            {
                _logger.LogInformation("Analyzing content for security issues, length: {Length}", 
                    request.Content?.Length ?? 0);
                
                var userId = GetUserId();
                var result = await _securityService.AnalyzeContentAsync(request.Content, userId, request.IpAddress);
                
                await _auditService.LogAsync(
                    userId,
                    "SecurityAnalysis",
                    "Security",
                    userId.ToString(),
                    null,
                    new { 
                        ContentLength = request.Content?.Length ?? 0, 
                        HasIssues = result.HasSecurityIssues,
                        IssuesCount = result.DetectedIssues?.Count ?? 0
                    });
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing content for security issues");
                return StatusCode(500, new { message = "Error analyzing content", error = ex.Message });
            }
        }

        /// <summary>
        /// Detects personally identifiable information (PII) in content
        /// </summary>
        [HttpPost("detect-pii")]
        [ProducesResponseType(typeof(PIIDetectionResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PIIDetectionResult>> DetectPII([FromBody] PIIDetectionRequest request)
        {
            try
            {
                _logger.LogInformation("Detecting PII in content, length: {Length}", 
                    request.Content?.Length ?? 0);
                
                var userId = GetUserId();
                // Note: Adjust call based on actual method signature
                var result = await _securityService.DetectPIIAsync(request.Content);
                
                await _auditService.LogAsync(
                    userId,
                    "PIIDetection",
                    "Security",
                    userId.ToString(),
                    null,
                    new {
                        ContentLength = request.Content?.Length ?? 0,
                        EntitiesFound = result.DetectedPII?.Count ?? 0,
                        SensitivityLevel = request.SensitivityLevel
                    });
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting PII in content");
                return StatusCode(500, new { message = "Error detecting PII", error = ex.Message });
            }
        }

        /// <summary>
        /// Gets recent security events for user
        /// </summary>
        [HttpGet("events")]
        [ProducesResponseType(typeof(List<SecurityEvent>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<SecurityEvent>>> GetSecurityEvents(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation("Getting security events for user {UserId}", userId);
                
                // Note: Adjust call based on actual method signature
                var events = await Task.FromResult(new List<SecurityEvent>()); // Placeholder
                
                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security events");
                return StatusCode(500, new { message = "Error retrieving security events", error = ex.Message });
            }
        }
    }

    /// <summary>
    /// Request model for content security analysis
    /// </summary>
    public class SecurityAnalysisRequest
    {
        /// <summary>
        /// Content to analyze
        /// </summary>
        [Required]
        public string Content { get; set; }
        
        /// <summary>
        /// IP address of the request origin (optional)
        /// </summary>
        public string IpAddress { get; set; }
    }

    /// <summary>
    /// Request model for PII detection
    /// </summary>
    public class PIIDetectionRequest
    {
        /// <summary>
        /// Content to scan for PII
        /// </summary>
        [Required]
        public string Content { get; set; }
        
        /// <summary>
        /// Sensitivity level (low, medium, high)
        /// </summary>
        public string SensitivityLevel { get; set; } = "medium";
    }
} 