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

namespace AI.API.Controllers
{
    /// <summary>
    /// AI Controller for text generation, embeddings, and streaming operations
    /// Provides core AI functionality for Chat and Document microservices
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/ai")]
    [Produces("application/json")]
    [ApiExplorerSettings(GroupName = "AI")]
    public class AIController : ControllerBase
    {
        private readonly IAIService _aiService;
        private readonly ILogger<AIController> _logger;

        /// <summary>
        /// Initializes a new instance of the AIController
        /// </summary>
        /// <param name="aiService">AI service for core operations</param>
        /// <param name="logger">Logger instance</param>
        public AIController(
            IAIService aiService,
            ILogger<AIController> logger)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Text Generation

        /// <summary>
        /// Generate AI response from prompt
        /// </summary>
        /// <param name="request">AI generation request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated answer response</returns>
        [HttpPost("generate")]
        [RateLimit(MaxRequests = 30, WindowInMinutes = 1)]
        [ProducesResponseType(typeof(AIResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<IActionResult> GenerateAnswerAsync(
            [FromBody] AIRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ErrorResponse 
                    { 
                        Message = "Invalid request", 
                        Details = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                    });
                }

                // Enrich request with user context
                request.UserId = User.GetUserId();
                request.SessionId ??= Request.Headers["X-Session-Id"].ToString();
                request.Source ??= Request.Headers["User-Agent"].ToString();

                var response = await _aiService.GenerateAnswerAsync(request, cancellationToken);
                return Ok(response);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Answer generation request was cancelled");
                return BadRequest(new ErrorResponse { Message = "Request was cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating answer for prompt: {Prompt}", request?.Prompt?.Substring(0, Math.Min(100, request.Prompt?.Length ?? 0)));
                return StatusCode(500, new ErrorResponse { Message = "Internal server error occurred" });
            }
        }



        #endregion

        #region Streaming Generation

        /// <summary>
        /// Stream generate answer - For real-time chat applications
        /// </summary>
        /// <param name="request">AI generation request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Streaming answer response</returns>
        [HttpPost("stream")]
        [Produces("text/plain")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task StreamGenerateAnswerAsync(
            [FromBody] AIRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    Response.StatusCode = 400;
                    await Response.WriteAsync("Invalid request", cancellationToken);
                    return;
                }

                // Enrich request with user context
                request.UserId = User.GetUserId();
                request.SessionId ??= Request.Headers["X-Session-Id"].ToString();
                request.Source ??= Request.Headers["User-Agent"].ToString();

                Response.ContentType = "text/plain";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";

                await foreach (var chunk in _aiService.StreamGenerateAnswerAsync(request, cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await Response.WriteAsync(chunk.Content ?? "", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Streaming answer generation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming answer generation");
                Response.StatusCode = 500;
                await Response.WriteAsync("Internal server error", cancellationToken);
            }
        }

        #endregion

        #region Embeddings

        /// <summary>
        /// Generate single embedding - Primary endpoint for Document microservice
        /// </summary>
        /// <param name="request">Embedding generation request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated embedding response</returns>
        [HttpPost("embedding")]
        [ProducesResponseType(typeof(EmbeddingResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<IActionResult> GenerateEmbeddingAsync(
            [FromBody] EmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ErrorResponse 
                    { 
                        Message = "Invalid request", 
                        Details = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                    });
                }

                // Enrich request with user context
                request.UserId = User.GetUserId();
                request.SessionId ??= Request.Headers["X-Session-Id"].ToString();
                request.Source ??= Request.Headers["User-Agent"].ToString();

                var response = await _aiService.GenerateEmbeddingAsync(request, cancellationToken);
                return Ok(response);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Embedding generation request was cancelled");
                return BadRequest(new ErrorResponse { Message = "Request was cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for document: {DocumentId}", request?.DocumentId);
                return StatusCode(500, new ErrorResponse { Message = "Internal server error occurred" });
            }
        }

        /// <summary>
        /// Generate batch embeddings - For bulk document processing
        /// </summary>
        /// <param name="request">Batch embedding generation request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated batch embedding response</returns>
        [HttpPost("embeddings/batch")]
        [ProducesResponseType(typeof(BatchEmbeddingResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<IActionResult> GenerateEmbeddingsBatchAsync(
            [FromBody] BatchEmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ErrorResponse 
                    { 
                        Message = "Invalid request", 
                        Details = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                    });
                }

                var response = await _aiService.GenerateEmbeddingsBatchAsync(request, cancellationToken);
                return Ok(response);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Batch embedding generation request was cancelled");
                return BadRequest(new ErrorResponse { Message = "Request was cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating batch embeddings");
                return StatusCode(500, new ErrorResponse { Message = "Internal server error occurred" });
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Validate model availability
        /// </summary>
        /// <param name="modelType">Model type to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Model availability status</returns>
        [HttpGet("models/{modelType}/validate")]
        [ProducesResponseType(typeof(bool), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        [ProducesResponseType(typeof(ErrorResponse), 500)]
        public async Task<IActionResult> ValidateModelAvailabilityAsync(
            string modelType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var isAvailable = await _aiService.ValidateModelAvailabilityAsync(modelType, cancellationToken);
                return Ok(isAvailable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating model availability for type: {ModelType}", modelType);
                return StatusCode(500, new ErrorResponse { Message = "Internal server error occurred" });
            }
        }

        #endregion
    }
}
