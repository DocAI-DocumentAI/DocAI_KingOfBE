using AI.API.Constants;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace AI.API.Controllers
{
    [ApiController]
    [Route(ApiEndPointConstant.ApiEndpoint)]
    public class AIController : ControllerBase
    {
        private readonly IOllamaAIService _ollamaAIService;
        private readonly ILogger<AIController> _logger;

        public AIController(IOllamaAIService ollamaAIService, ILogger<AIController> logger)
        {
            _ollamaAIService = ollamaAIService;
            _logger = logger;
        }
        [HttpPost("generate")] // REVIEW POINT: Specific endpoint for AI generation
        [ProducesResponseType(typeof(AIResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IAsyncEnumerable<string>), StatusCodes.Status200OK)] // For streaming responses
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Generate([FromBody] AIRequest request)
        {
            if (string.IsNullOrEmpty(request.Question))
            {
                _logger.LogError("Generate request received with empty question.");
                return BadRequest("Question cannot be empty.");
            }

            try
            {
                if (request.StreamResponse)
                {
                    _logger.LogInformation("Streaming response requested for AI generation.");
                    return Ok(_ollamaAIService.StreamGenerateResponseAsync(request));
                }
                else
                {
                    _logger.LogInformation("Non-streaming response requested for AI generation.");
                    var response = await _ollamaAIService.GenerateResponseAsync(request);
                    // Return 200 OK with the generated response.
                    return Ok(response);
                }
            }
            catch (ApplicationException appEx) 
            {
                _logger.LogError(appEx, "Application error during AI generation.");
                // Use Problem() for consistency with your AuthController's Problem() usage
                return Problem(detail: appEx.Message, statusCode: StatusCodes.Status500InternalServerError, title: "AI Generation Error");
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "An unexpected error occurred during AI generation.");
                return Problem(detail: "An unexpected error occurred. Please try again later.", statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected Server Error");
            }
        }
        // REVIEW POINT: Thêm Endpoint mới cho Embedding Generation
        [HttpPost("embeddings")] // Endpoint cho Embedding Generation
        [ProducesResponseType(typeof(EmbeddingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateEmbedding([FromBody] EmbeddingRequest request)
        {
            if (string.IsNullOrEmpty(request.Text))
            {
                _logger.LogError("Embedding request received with empty text.");
                return BadRequest("Input text for embedding cannot be empty.");
            }

            try
            {
                _logger.LogInformation($"Embedding generation requested for text (length: {request.Text.Length}).");
                var response = await _ollamaAIService.GenerateEmbeddingAsync(request);
                return Ok(response);
            }
            catch (ArgumentException argEx) // Bắt lỗi ArgumentException cụ thể từ service
            {
                _logger.LogError(argEx, "Invalid argument for embedding generation.");
                return BadRequest(argEx.Message);
            }
            catch (ApplicationException appEx)
            {
                _logger.LogError(appEx, "Application error during embedding generation.");
                return Problem(detail: appEx.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Embedding Generation Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during embedding generation.");
                return Problem(detail: "An unexpected error occurred. Please try again later.", statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected Server Error");
            }
        }
    }
}
