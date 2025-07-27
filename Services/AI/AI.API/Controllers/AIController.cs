using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AI.API.Atributte;
using AI.API.Extensions;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AutoMapper;
using System.Security.Claims;
using System.Text.Json;
using AI.Domain.Models;

namespace AI.API.Controllers
{
    [ApiController]
    [Route("api/ai")]
    [Produces("application/json")]
    public class AIController : ControllerBase
    {
        private readonly IAIService _aiService;
        private readonly IMapper _mapper;
        private readonly ILogger<AIController> _logger;
        public AIController(
           IAIService aiService,
           IMapper mapper,
           ILogger<AIController> logger)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        #region Text Generation

        [HttpPost("generate")]
        [RateLimit(MaxRequests = 30, WindowInMinutes = 1)]
        public async Task<IActionResult> GenerateAsync(
              [FromBody] GenerateRequest request,
              CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = User.FindFirstValue("userId") ?? "anonymous";
                request.UserId = userId;

                AIResponse response;

                if (request.Context?.Any() == true || request.ConversationHistory?.Any() == true)
                {
                    var contextRequest = _mapper.Map<AIContextRequest>(request);
                    response = await _aiService.GenerateWithContextAsync(contextRequest, cancellationToken);
                }
                else if (!string.IsNullOrEmpty(request.ModelId))
                {
                    var aiRequest = _mapper.Map<AIRequest>(request);
                    response = await _aiService.GenerateWithModelAsync(request.ModelId, aiRequest, cancellationToken);
                }
                else
                {
                    var aiRequest = _mapper.Map<AIRequest>(request);
                    response = await _aiService.GenerateAnswerAsync(aiRequest, cancellationToken);
                }

                return Ok(response);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Answer generation request was cancelled for user: {UserId}", request?.UserId);
                return BadRequest(new ErrorResponse { Message = "Request was cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating answer for user: {UserId}, prompt: {Prompt}", request?.UserId, request?.Prompt?.Substring(0, Math.Min(100, request?.Prompt?.Length ?? 0)));
                return StatusCode(500, new ErrorResponse { Message = "Internal server error occurred" });
            }
        }

        [HttpPost("generate/basic")]
        [RateLimit(MaxRequests = 30, WindowInMinutes = 1)]
        public async Task<IActionResult> GenerateBasicAsync(
          [FromBody] AIRequest request,
          CancellationToken cancellationToken = default)
        {
            try
            {
                // Enrich request with user context
                request.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

                var response = await _aiService.GenerateAnswerAsync(request, cancellationToken);
                return Ok(response);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Basic generation request was cancelled for user: {UserId}", request?.UserId);
                return BadRequest(new ErrorResponse { Message = "Request was cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in basic generation for user: {UserId}", request?.UserId);
                return StatusCode(500, new ErrorResponse { Message = "Internal server error occurred" });
            }
        }

        #endregion

        #region Streaming Generation
        [HttpPost("stream")]
        [RateLimit(MaxRequests = 10, WindowInMinutes = 1)]
        public async Task StreamAsync(
           [FromBody] StreamRequest request,
           CancellationToken cancellationToken = default)
        {
            try
            {

                // Enrich request with user context
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
                request.UserId = userId;

                Response.ContentType = "text/plain; charset=utf-8";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";
                Response.Headers["Access-Control-Allow-Origin"] = "*";

                // Check if this is a contextual request
                if (request.Context?.Any() == true || request.ConversationHistory?.Any() == true)
                {
                    var contextRequest = _mapper.Map<AIContextRequest>(request);

                    await foreach (var chunk in _aiService.StreamWithContextAsync(contextRequest, cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var json = JsonSerializer.Serialize(chunk);
                        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);

                        if (chunk.IsComplete) break;
                    }
                }
                else
                {
                    var aiRequest = _mapper.Map<AIRequest>(request);

                    await foreach (var chunk in _aiService.StreamGenerateAnswerAsync(aiRequest, cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var json = JsonSerializer.Serialize(chunk);
                        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);

                        if (chunk.IsComplete) break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Streaming generation was cancelled for user: {UserId}", request?.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming generation for user: {UserId}", request?.UserId);
                Response.StatusCode = 500;

                var errorChunk = new StreamChunk
                {
                    Content = $"Error: {ex.Message}",
                    IsComplete = true,
                    RequestId = Guid.NewGuid().ToString("N")[..8]
                };

                var json = JsonSerializer.Serialize(errorChunk);
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            }
        }

        #endregion

        #region Model Management

        [HttpGet("models")]
        [ProducesResponseType(typeof(List<AIModel>), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<List<AIModel>> GetModelsAsync()
        {
            try
            {
                return await _aiService.GetAvailableModelsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models");
                return new List<AIModel>();
            }
        }
        [HttpGet("models/{modelId}/capabilities")]
        public async Task<IActionResult> GetModelCapabilitiesAsync(string modelId)
        {
            try
            {
                var capabilities = await _aiService.GetModelCapabilitiesAsync(modelId);

                if (capabilities == null || !capabilities.SupportsTextGeneration)
                {
                    return NotFound($"Model {modelId} not found or not available");
                }

                return Ok(capabilities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model capabilities for {ModelId}", modelId);
                return StatusCode(500, ex.Message);
            }
        }
        [HttpPost("models/switch/validate")]
        public async Task<ModelSwitchResponse> ValidateModelSwitchAsync([FromBody] ModelSwitchRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
                request.UserId = userId;

                var models = await _aiService.GetAvailableModelsAsync();
                var targetModel = models.FirstOrDefault(m => m.Id == request.NewModelId);

                if (targetModel == null || !targetModel.IsAvailable)
                {
                    return new ModelSwitchResponse
                    {
                        Success = false,
                        Message = "Model not available",
                        SessionId = request.SessionId
                    };
                }

                _logger.LogInformation("Model switch validated for user {UserId}: {ModelId}", userId, request.NewModelId);

                return new ModelSwitchResponse
                {
                    Success = true,
                    Message = "Model switch validated successfully",
                    NewModel = request.NewModelId,
                    SessionId = request.SessionId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating model switch");
                return new ModelSwitchResponse
                {
                    Success = false,
                    Message = ex.Message,
                    SessionId = request.SessionId
                };
            }
        }
        [HttpGet("models/{modelType}/validate")]
        public async Task<bool> ValidateModelAvailabilityAsync(string modelType, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _aiService.ValidateModelAvailabilityAsync(modelType, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating model availability for type: {ModelType}", modelType);
                return false;
            }
        }
        #endregion

        #region Utility Features

        [HttpPost("tokens/count")]
        public async Task<TokenCountResult> CountTokensAsync([FromBody] TokenCountRequest request)
        {
            try
            {
                return await _aiService.CountTokensAsync(request.Text, request.Model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting tokens");
                return new TokenCountResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
        [HttpPost("intent/detect")]
        public async Task<IntentResult> DetectIntentAsync([FromBody] IntentDetectionRequest request)
        {
            try
            {
                return await _aiService.DetectIntentAsync(request.Text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting intent");
                return new IntentResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
        [HttpPost("title/suggest")]
        public async Task<string> SuggestTitleAsync([FromBody] TitleSuggestionRequest request)
        {
            try
            {
                return await _aiService.SuggestTitleAsync(request.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suggesting title");
                return "Cuộc trò chuyện mới";
            }
        }
        #endregion

        #region Embeddings

        [HttpPost("embeddings")]
        public async Task<EmbeddingResponse> GenerateEmbeddingAsync([FromBody] EmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                request.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
                return await _aiService.GenerateEmbeddingAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Embedding generation cancelled");
                return new EmbeddingResponse
                {
                    Success = false,
                    Message = "Request was cancelled",
                    RequestId = Guid.NewGuid().ToString("N")[..8]
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for document: {DocumentId}", request?.DocumentId);
                return new EmbeddingResponse
                {
                    Success = false,
                    Message = ex.Message,
                    RequestId = Guid.NewGuid().ToString("N")[..8]
                };
            }
        }
        [HttpPost("embeddings/batch")]
        [RateLimit(MaxRequests = 10, WindowInMinutes = 1)]
        public async Task<BatchEmbeddingResponse> GenerateEmbeddingsBatchAsync([FromBody] BatchEmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _aiService.GenerateEmbeddingsBatchAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Batch embedding generation cancelled");
                return new BatchEmbeddingResponse
                {
                    Success = false,
                    Message = "Request was cancelled",
                    RequestId = Guid.NewGuid().ToString("N")[..8]
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating batch embeddings");
                return new BatchEmbeddingResponse
                {
                    Success = false,
                    Message = ex.Message,
                    RequestId = Guid.NewGuid().ToString("N")[..8]
                };
            }
        }
        #endregion 
    }
}
