using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatBox.API.Services.Interfaces;
using ChatBox.API.Payload.Request.AIClientService;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using System.Security.Claims;

namespace ChatBox.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class AIController : ControllerBase
    {
        private readonly IAiServiceClient _aiService;
        private readonly ILogger<AIController> _logger;

        public AIController(IAiServiceClient aiService, ILogger<AIController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        // Core Text Generation
        [HttpPost("generate")]
        public async Task<ActionResult<AiGenerationResult>> GenerateResponse([FromBody] AdvancedAiGenerationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var response = await _aiService.GenerateResponseAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("stream")]
        public async Task<ActionResult> StreamResponse([FromBody] StreamingRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var streamingResponse = await _aiService.StreamResponseAsync(request);
                
                Response.Headers.Add("Content-Type", "text/event-stream");
                Response.Headers.Add("Cache-Control", "no-cache");
                Response.Headers.Add("Connection", "keep-alive");

                await foreach (var chunk in streamingResponse)
                {
                    var data = System.Text.Json.JsonSerializer.Serialize(chunk);
                    await Response.WriteAsync($"data: {data}\n\n");
                    await Response.Body.FlushAsync();
                }

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming AI response");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Token Management
        [HttpPost("tokens/count")]
        public async Task<ActionResult<int>> CountTokens([FromBody] TokenCountRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Text))
                {
                    return BadRequest(new { message = "Text is required" });
                }

                var tokenCount = await _aiService.CountTokensAsync(request.Text, request.Model ?? "default");
                return Ok(new { tokenCount, text = request.Text, model = request.Model ?? "default" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting tokens");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("tokens/estimate")]
        public async Task<ActionResult<TokenBreakdown>> EstimateTokenUsage([FromBody] EstimateTokenRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var breakdown = await _aiService.EstimateFullTokenUsageAsync(request);
                return Ok(breakdown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating token usage");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("tokens/truncate")]
        public async Task<ActionResult<string>> TruncateToTokenLimit([FromBody] TruncateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Text))
                {
                    return BadRequest(new { message = "Text is required" });
                }

                if (request.MaxTokens <= 0)
                {
                    return BadRequest(new { message = "MaxTokens must be greater than 0" });
                }

                var truncatedText = await _aiService.TruncateToTokenLimitAsync(request.Text, request.MaxTokens);
                return Ok(new { 
                    originalText = request.Text, 
                    truncatedText, 
                    maxTokens = request.MaxTokens 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error truncating text");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Content Analysis
        [HttpPost("analyze")]
        public async Task<ActionResult<MessageAnalysisResult>> AnalyzeContent([FromBody] ContentAnalysisRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var analysis = await _aiService.AnalyzeContentAsync(request);
                return Ok(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing content");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("detect-language")]
        public async Task<ActionResult<string>> DetectLanguage([FromBody] LanguageDetectionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Content))
                {
                    return BadRequest(new { message = "Content is required" });
                }

                var language = await _aiService.DetectLanguageAsync(request.Content);
                return Ok(new { content = request.Content, detectedLanguage = language });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting language");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Conversation Features
        [HttpPost("summarize")]
        public async Task<ActionResult<ConversationSummaryResult>> GenerateConversationSummary([FromBody] ConversationSummaryRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var summary = await _aiService.GenerateConversationSummaryAsync(request);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating conversation summary");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // Smart Features
        [HttpPost("detect-intent")]
        public async Task<ActionResult<IntentDetectionResult>> DetectIntent([FromBody] IntentDetectionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var intent = await _aiService.DetectIntentAsync(request);
                return Ok(intent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting intent");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("translate")]
        public async Task<ActionResult<string>> TranslateText([FromBody] TranslationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var translation = await _aiService.TranslateTextAsync(request);
                return Ok(new {
                    originalText = request.Text,
                    translatedText = translation,
                    sourceLanguage = request.SourceLanguage,
                    targetLanguage = request.TargetLanguage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating text");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }


    }

    // Helper request classes
    public class TokenCountRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? Model { get; set; }
    }

    public class TruncateRequest
    {
        public string Text { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
    }

    public class LanguageDetectionRequest
    {
        public string Content { get; set; } = string.Empty;
    }
}
