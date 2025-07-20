using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AI.API.Atributte;
using AI.API.Constants;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.API.Controllers
{
    [Authorize]
    //[Authorize] 
    public class AIController : BaseApiController
    {
        private readonly IAIService _aiService;
        private readonly ILogger<AIController> _logger;

        public AIController(
            IAIService aiService,
            ILogger<AIController> logger)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        /// <summary>
        /// Generate AI response based on documents and question
        /// </summary>
        [HttpPost("generate")]
        [RateLimit("ai-generation", limit: 20, windowSeconds: 60)]
        [ProducesResponseType(typeof(AIResponse), 200)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(typeof(object), 429)]
        [ProducesResponseType(typeof(object), 500)]
        public async Task<IActionResult> GenerateAnswer([FromBody][Required] AIRequest request)
        {
            try
            {
                // Add user context
                var userId = User.Identity?.Name ?? HttpContext.Connection.RemoteIpAddress?.ToString();
                request.UserId = userId;

                // Add request metadata
                request.Metadata ??= new Dictionary<string, object>();
                request.Metadata["requestTime"] = DateTime.UtcNow;
                request.Metadata["userAgent"] = Request.Headers["User-Agent"].ToString();

                var response = await _aiService.GenerateAnswerAsync(request);

                if (!response.Success)
                {
                    _logger.LogWarning("AI generation failed for user {UserId}: {Message}",
                        userId, response.Message);
                    return StatusCode(500, response);
                }

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response");
                return HandleError(ex, "Failed to generate AI response");
            }
        }

        /// <summary>
        /// Stream AI response in real-time
        /// </summary>
        [HttpPost("generate/stream")]
        [RateLimit("ai-stream", limit: 10, windowSeconds: 60)]
        [Produces("text/event-stream")]
        public async Task StreamGenerateAnswer([FromBody][Required] AIRequest request)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");
            Response.Headers.Add("X-Accel-Buffering", "no"); // Disable nginx buffering

            try
            {
                // Add user context
                var userId = User.Identity?.Name ?? HttpContext.Connection.RemoteIpAddress?.ToString();
                request.UserId = userId;

                await Response.WriteAsync($"event: start\ndata: {{\"message\":\"Starting generation\"}}\n\n");
                await Response.Body.FlushAsync();

                var chunkCount = 0;
                await foreach (var chunk in _aiService.StreamGenerateAnswerAsync(request, HttpContext.RequestAborted))
                {
                    chunkCount++;

                    // Send chunk
                    var eventData = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        content = chunk.Content,
                        isComplete = chunk.IsComplete,
                        tokenCount = chunk.TokenCount,
                        chunkIndex = chunkCount
                    });

                    await Response.WriteAsync($"event: chunk\ndata: {eventData}\n\n");
                    await Response.Body.FlushAsync();

                    // Send heartbeat every 10 chunks
                    if (chunkCount % 10 == 0)
                    {
                        await Response.WriteAsync($"event: heartbeat\ndata: {{\"chunks\":{chunkCount}}}\n\n");
                        await Response.Body.FlushAsync();
                    }
                }

                await Response.WriteAsync($"event: complete\ndata: {{\"totalChunks\":{chunkCount}}}\n\n");
                await Response.Body.FlushAsync();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Stream generation cancelled by client");
                await Response.WriteAsync("event: cancelled\ndata: {\"message\":\"Stream cancelled\"}\n\n");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stream generation");
                await Response.WriteAsync($"event: error\ndata: {{\"error\":\"{ex.Message}\"}}\n\n");
            }
        }


        /// <summary>
        /// Generate embedding for a document
        /// </summary>
        [HttpPost("embeddings")]
        [RateLimit("embeddings", limit: 50, windowSeconds: 60)]
        [ProducesResponseType(typeof(EmbeddingResponse), 200)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(typeof(object), 500)]
        public async Task<IActionResult> GenerateEmbedding([FromBody][Required] EmbeddingRequest request)
        {
            try
            {
                var response = await _aiService.GenerateEmbeddingAsync(request);

                if (!response.Success)
                {
                    return StatusCode(500, response);
                }

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding");
                return HandleError(ex, "Failed to generate embedding");
            }
        }

        /// <summary>
        /// Generate embeddings for multiple documents
        /// </summary>
        [HttpPost("embeddings/batch")]
        [RateLimit("embeddings-batch", limit: 10, windowSeconds: 60)]
        [ProducesResponseType(typeof(BatchEmbeddingResponse), 200)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(typeof(object), 500)]
        public async Task<IActionResult> GenerateEmbeddingsBatch([FromBody][Required] BatchEmbeddingRequest request)
        {
            try
            {
                if (request.Documents?.Any() != true)
                {
                    return HandleBadRequest("At least one document is required");
                }

                var response = await _aiService.GenerateEmbeddingsBatchAsync(request, HttpContext.RequestAborted);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return HandleBadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating batch embeddings");
                return HandleError(ex, "Failed to generate batch embeddings");
            }
        }
        /// <summary>
        /// Validate if a model is available
        /// </summary>
        [HttpGet("models/{modelType}/validate")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> ValidateModel(string modelType)
        {
            try
            {
                var isAvailable = await _aiService.ValidateModelAvailabilityAsync(modelType);

                if (!isAvailable)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Model type '{modelType}' is not available",
                        modelType
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = $"Model type '{modelType}' is available",
                    modelType,
                    validated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating model {ModelType}", modelType);
                return HandleError(ex, $"Failed to validate model {modelType}");
            }
        }
    }
}

