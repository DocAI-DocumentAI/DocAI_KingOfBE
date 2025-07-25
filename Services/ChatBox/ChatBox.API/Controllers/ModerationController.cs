using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Services.Interfaces;
using ChatBox.API.Payload.Response.ContentModerationServiceResponse;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class ModerationController : ControllerBase
    {
        private readonly IContentModerationService _moderationService;
        private readonly ILogger<ModerationController> _logger;

        public ModerationController(
            IContentModerationService moderationService, 
            ILogger<ModerationController> logger)
        {
            _moderationService = moderationService;
            _logger = logger;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        /// <summary>
        /// Moderate content for safety and compliance
        /// </summary>
        [HttpPost("moderate")]
        public async Task<ActionResult<ContentModerationResponse>> ModerateContent([FromBody] ModerationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Content))
                {
                    return BadRequest(new { message = "Content is required" });
                }

                var userId = request.UserId ?? GetUserId();
                var response = await _moderationService.ModerateContentAsync(request.Content, userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moderating content");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Quick safety check for content
        /// </summary>
        [HttpPost("check-safety")]
        public async Task<ActionResult<SafetyCheckResponse>> CheckContentSafety([FromBody] SafetyCheckRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Content))
                {
                    return BadRequest(new { message = "Content is required" });
                }

                var isSafe = await _moderationService.IsContentSafeAsync(request.Content);
                return Ok(new SafetyCheckResponse 
                { 
                    Content = request.Content,
                    IsSafe = isSafe,
                    CheckedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking content safety");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Detect prohibited terms in content
        /// </summary>
        [HttpPost("detect-terms")]
        public async Task<ActionResult<ProhibitedTermsResponse>> DetectProhibitedTerms([FromBody] TermDetectionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Content))
                {
                    return BadRequest(new { message = "Content is required" });
                }

                var terms = await _moderationService.DetectProhibitedTermsAsync(request.Content);
                return Ok(new ProhibitedTermsResponse
                {
                    Content = request.Content,
                    ProhibitedTerms = terms,
                    HasProhibitedTerms = terms.Any(),
                    CheckedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting prohibited terms");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Check if user is flagged for moderation
        /// </summary>
        [HttpGet("user-status")]
        public async Task<ActionResult<UserModerationStatus>> GetUserModerationStatus()
        {
            try
            {
                var userId = GetUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized(new { message = "Invalid user ID" });
                }

                var isFlagged = await _moderationService.IsUserFlaggedAsync(userId);
                return Ok(new UserModerationStatus
                {
                    UserId = userId,
                    IsFlagged = isFlagged,
                    CheckedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user moderation status");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Check if specific user is flagged (Admin only)
        /// </summary>
        [HttpGet("user-status/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserModerationStatus>> GetUserModerationStatus(Guid userId)
        {
            try
            {
                var isFlagged = await _moderationService.IsUserFlaggedAsync(userId);
                return Ok(new UserModerationStatus
                {
                    UserId = userId,
                    IsFlagged = isFlagged,
                    CheckedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user moderation status for user {UserId}", userId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update moderation rules (Admin only)
        /// </summary>
        [HttpPut("rules")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<bool>> UpdateModerationRules([FromBody] UpdateRulesRequest request)
        {
            try
            {
                if (request.Rules == null || !request.Rules.Any())
                {
                    return BadRequest(new { message = "Rules are required" });
                }

                await _moderationService.UpdateModerationRulesAsync(request.Rules);
                return Ok(new { success = true, message = "Moderation rules updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating moderation rules");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }


    }

    // Helper request/response classes
    public class ModerationRequest
    {
        public string Content { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string? Context { get; set; }
    }

    public class SafetyCheckRequest
    {
        public string Content { get; set; } = string.Empty;
    }

    public class SafetyCheckResponse
    {
        public string Content { get; set; } = string.Empty;
        public bool IsSafe { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    public class TermDetectionRequest
    {
        public string Content { get; set; } = string.Empty;
    }

    public class ProhibitedTermsResponse
    {
        public string Content { get; set; } = string.Empty;
        public List<string> ProhibitedTerms { get; set; } = new();
        public bool HasProhibitedTerms { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    public class UserModerationStatus
    {
        public Guid UserId { get; set; }
        public bool IsFlagged { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    public class UpdateRulesRequest
    {
        public List<ModerationRule> Rules { get; set; } = new();
    }

    public class ModerationStatistics
    {
        public int TotalContentChecked { get; set; }
        public int ContentBlocked { get; set; }
        public int ContentApproved { get; set; }
        public int UsersWarned { get; set; }
        public int UsersBanned { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public Dictionary<string, int> ViolationsByType { get; set; } = new();
    }
}
