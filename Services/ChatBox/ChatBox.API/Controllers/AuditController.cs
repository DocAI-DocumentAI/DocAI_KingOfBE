using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class AuditController : ControllerBase
    {
        private readonly IAuditService _auditService;
        private readonly ILogger<AuditController> _logger;

        public AuditController(IAuditService auditService, ILogger<AuditController> logger)
        {
            _auditService = auditService;
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

        private string GetUserAgent()
        {
            return HttpContext.Request.Headers["User-Agent"].ToString() ?? "unknown";
        }

        /// <summary>
        /// Log an audit event
        /// </summary>
        [HttpPost("log")]
        public async Task<ActionResult<bool>> LogAuditEvent([FromBody] AuditLogRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = request.UserId ?? GetUserId();
                var ipAddress = GetIpAddress();
                var userAgent = GetUserAgent();

                await _auditService.LogAsync(
                    userId, 
                    request.Action, 
                    request.EntityType, 
                    request.EntityId,
                    request.OldValues, 
                    request.NewValues, 
                    ipAddress, 
                    userAgent);

                return Ok(new { success = true, message = "Audit event logged successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit event");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Log a security event
        /// </summary>
        [HttpPost("security")]
        public async Task<ActionResult<bool>> LogSecurityEvent([FromBody] SecurityAuditRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = request.UserId ?? GetUserId();
                var ipAddress = GetIpAddress();

                await _auditService.LogSecurityEventAsync(
                    userId,
                    request.EventType,
                    request.Description,
                    request.Severity ?? "medium",
                    ipAddress,
                    request.Metadata);

                return Ok(new { success = true, message = "Security event logged successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging security event");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get audit logs for current user
        /// </summary>
        [HttpGet("user")]
        public async Task<ActionResult<List<AuditLog>>> GetUserAuditLogs(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int limit = 100)
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { message = "Invalid user ID" });
                }

                var logs = await _auditService.GetUserAuditLogsAsync(userId, fromDate, toDate, limit);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user audit logs");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get audit logs for specific user (Admin only)
        /// </summary>
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<AuditLog>>> GetUserAuditLogs(
            Guid userId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int limit = 100)
        {
            try
            {
                var logs = await _auditService.GetUserAuditLogsAsync(userId, fromDate, toDate, limit);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs for user {UserId}", userId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get system audit logs (Admin only)
        /// </summary>
        [HttpGet("system")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<AuditLog>>> GetSystemAuditLogs(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int limit = 1000)
        {
            try
            {
                var logs = await _auditService.GetSystemAuditLogsAsync(fromDate, toDate, limit);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system audit logs");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Search audit logs (Admin only)
        /// </summary>
        [HttpPost("search")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<AuditLog>>> SearchAuditLogs([FromBody] AuditSearchRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SearchTerm))
                {
                    return BadRequest(new { message = "Search term is required" });
                }

                var logs = await _auditService.SearchAuditLogsAsync(
                    request.SearchTerm, 
                    request.FromDate, 
                    request.ToDate, 
                    request.Limit);

                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching audit logs");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }



        /// <summary>
        /// Cleanup old audit logs (Admin only)
        /// </summary>
        [HttpPost("cleanup")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<bool>> CleanupOldLogs([FromBody] CleanupRequest request)
        {
            try
            {
                await _auditService.CleanupOldLogsAsync(request.RetentionDays);
                return Ok(new { success = true, message = $"Cleanup completed for logs older than {request.RetentionDays} days" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old audit logs");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    // Helper request/response classes
    public class AuditLogRequest
    {
        public Guid? UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public object? OldValues { get; set; }
        public object? NewValues { get; set; }
    }

    public class SecurityAuditRequest
    {
        public Guid? UserId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Severity { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class AuditSearchRequest
    {
        public string SearchTerm { get; set; } = string.Empty;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Limit { get; set; } = 100;
    }

    public class AuditExportRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<string> EntityTypes { get; set; } = new();
        public List<string> Actions { get; set; } = new();
        public string Format { get; set; } = "json"; // json, csv, excel
        public bool IncludeUserDetails { get; set; } = true;
    }

    public class CleanupRequest
    {
        public int RetentionDays { get; set; } = 90;
    }

    public class AuditStatistics
    {
        public int TotalLogs { get; set; }
        public int UniqueUsers { get; set; }
        public int UniqueActions { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public Dictionary<string, int> LogsByAction { get; set; } = new();
        public Dictionary<string, int> LogsByEntityType { get; set; } = new();
        public Dictionary<string, int> LogsByDay { get; set; } = new();
    }

    public class AuditExportResult
    {
        public string ExportId { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public DateTime ExportedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
