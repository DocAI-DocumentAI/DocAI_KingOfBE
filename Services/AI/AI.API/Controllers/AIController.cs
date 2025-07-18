using System.Text;
using System.Text.Json;
using AI.API.Constants;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.KernelMemory.AI;

namespace AI.API.Controllers
{
    [ApiController]
    [Route(ApiEndPointConstant.ApiEndpoint)]
    //[Authorize] 
    public class AIController : ControllerBase
    {
        private readonly IAIService _aiService;
        private readonly ILogger<AIController> _logger;
        public AIController(IAIService aiService, ILogger<AIController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        [HttpPost("generate")] // Endpoint cho Chat Completion
        [ProducesResponseType(typeof(AIResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IAsyncEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AIResponse>> GenerateAnswer([FromBody] AIRequest request)
        {
            if (string.IsNullOrEmpty(request.Question))
            {
                return BadRequest("Question is required");
            }

            if (request.StreamResponse)
            {
                return BadRequest("Use /stream-answer endpoint for streaming responses");
            }

            var result = await _aiService.GenerateAnswerAsync(request);

            if (!result.Success)
            {
                return StatusCode(500, result);
            }

            return Ok(result);
        }

        [HttpPost("stream-answer")] 
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task StreamAnswer([FromBody] AIRequest request)
        {
            if (string.IsNullOrEmpty(request.Question))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Question is required");
                return;
            }

            Response.Headers.Add("Content-Type", "text/event-stream");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");

            try
            {
                await foreach (var chunk in _aiService.StreamGenerateAnswerAsync(request, HttpContext.RequestAborted))
                {
                    var data = $"data: {JsonSerializer.Serialize(new { text = chunk })}\n\n";
                    await Response.WriteAsync(data);
                    await Response.Body.FlushAsync();
                }

                await Response.WriteAsync("data: [DONE]\n\n");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Stream generation cancelled by client");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stream generation");
                await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { error = ex.Message })}\n\n");
            }
        }

        [HttpPost("embeddings")] // Endpoint cho Embedding Generation
        [ProducesResponseType(typeof(EmbeddingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<EmbeddingResponse>> GenerateEmbedding([FromBody] EmbeddingRequest request)
        {
            if (string.IsNullOrEmpty(request.Content))
            {
                return BadRequest("Content is required");
            }

            if (string.IsNullOrEmpty(request.DocumentId))
            {
                return BadRequest("DocumentId is required");
            }

            var result = await _aiService.GenerateEmbeddingAsync(request);

            if (!result.Success)
            {
                return StatusCode(500, result);
            }

            return Ok(result);
        }
        [HttpPost("generate-embeddings-batch")]
        [ProducesResponseType(typeof(EmbeddingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BatchEmbeddingResponse>> GenerateEmbeddingsBatch([FromBody] BatchEmbeddingRequest request)
        {
            if (request.Documents == null || !request.Documents.Any())
            {
                return BadRequest("Documents array is required");
            }

            // Validate all documents have required fields
            var invalidDocs = request.Documents.Where(d => string.IsNullOrEmpty(d.Content) || string.IsNullOrEmpty(d.DocumentId)).ToList();
            if (invalidDocs.Any())
            {
                return BadRequest($"All documents must have Content and DocumentId. Invalid documents: {string.Join(", ", invalidDocs.Select(d => d.DocumentId))}");
            }

            var result = await _aiService.GenerateEmbeddingsBatchAsync(request);
            return Ok(result);
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "AI Microservice"
            });
        }
    }
}
