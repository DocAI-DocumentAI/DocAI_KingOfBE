using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Services.Interfaces;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Request.AIClientService;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{
    /// <summary>
    /// Controller for token validation and optimization operations
    /// </summary>
    [ApiController]
    [Route("api/v1/token")]
    [Authorize]
    public class TokenValidationController : ControllerBase
    {
        private readonly ITokenValidationService _tokenValidationService;
        private readonly IAuditService _auditService;
        private readonly ILogger<TokenValidationController> _logger;

        public TokenValidationController(
            ITokenValidationService tokenValidationService,
            IAuditService auditService,
            ILogger<TokenValidationController> logger)
        {
            _tokenValidationService = tokenValidationService;
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

        /// <summary>
        /// Estimates token usage for a given input, system prompt, and conversation history
        /// </summary>
        [HttpPost("estimate")]
        [ProducesResponseType(typeof(TokenBreakdown), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TokenBreakdown>> EstimateTokens([FromBody] EstimateTokenRequest request)
        {
            try
            {
                _logger.LogInformation("Estimating tokens for message with length {Length}", request.Message?.Length ?? 0);
                
                var tokenBreakdown = await _tokenValidationService.EstimateTokenUsageAsync(
                    request.Message,
                    request.SystemPrompt,
                    request.ConversationHistory);

                var userId = GetUserId();
                await _auditService.LogAsync(
                    userId,
                    "EstimateTokens",
                    "TokenValidation",
                    userId.ToString(),
                    null,
                    new { InputLength = request.Message?.Length ?? 0, Result = tokenBreakdown.TotalEstimatedTokens });
                    
                return Ok(tokenBreakdown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating tokens");
                return StatusCode(500, new { message = "Error estimating token usage", error = ex.Message });
            }
        }

        /// <summary>
        /// Validates if content is within the specified token limit
        /// </summary>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(TokenValidationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TokenValidationResult>> ValidateTokenLimit([FromBody] ValidateTokenLimitRequest request)
        {
            try
            {
                _logger.LogInformation("Validating token limit for content length {Length}", request.Content?.Length ?? 0);
                
                var isWithinLimit = await _tokenValidationService.IsWithinTokenLimitAsync(
                    request.Content, 
                    request.MaxTokens);
                
                var result = new TokenValidationResult
                {
                    IsValid = isWithinLimit,
                    MaxTokens = request.MaxTokens,
                    Message = isWithinLimit 
                        ? "Content is within token limit" 
                        : "Content exceeds token limit",
                    Timestamp = DateTime.UtcNow
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token limit");
                return StatusCode(500, new { message = "Error validating token limit", error = ex.Message });
            }
        }

        /// <summary>
        /// Optimizes content to fit within token limits using specified strategy
        /// </summary>
        [HttpPost("optimize")]
        [ProducesResponseType(typeof(OptimizedContent), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OptimizedContent>> OptimizeContent([FromBody] OptimizeContentRequest request)
        {
            try
            {
                _logger.LogInformation("Optimizing content with length {Length} for token limit {MaxTokens}",
                    request.Content?.Length ?? 0, request.MaxTokens);
                
                var result = await _tokenValidationService.OptimizeContentForTokenLimitAsync(
                    request.Content,
                    request.MaxTokens,
                    request.OptimizationStrategy);
                
                var userId = GetUserId();
                await _auditService.LogAsync(
                    userId,
                    "OptimizeContent", 
                    "TokenValidation", 
                    userId.ToString(), 
                    null,
                    new { Strategy = request.OptimizationStrategy, TokensSaved = result.TokensSaved });
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing content");
                return StatusCode(500, new { message = "Error optimizing content", error = ex.Message });
            }
        }

        /// <summary>
        /// Gets token usage statistics for the current user
        /// </summary>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(List<TokenUsageStats>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<TokenUsageStats>>> GetTokenUsageStats(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation("Getting token usage stats for user {UserId}", userId);
                
                var stats = await _tokenValidationService.GetTokenUsageStatsAsync(userId, fromDate, toDate);
                
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token usage stats");
                return StatusCode(500, new { message = "Error retrieving token usage statistics", error = ex.Message });
            }
        }
    }

    /// <summary>
    /// Result of token validation check
    /// </summary>
    public class TokenValidationResult
    {
        /// <summary>
        /// Whether the content is within the token limit
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// Maximum allowed tokens
        /// </summary>
        public int MaxTokens { get; set; }
        
        /// <summary>
        /// Validation result message
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// When the validation was performed
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
} 