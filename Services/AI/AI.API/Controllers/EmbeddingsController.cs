using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AI.API.Controllers
{
    [ApiController]
    [Route("api/embeddings")]
    public class EmbeddingsController : ControllerBase
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<EmbeddingsController> _logger;

        public EmbeddingsController(IEmbeddingService embeddingService, ILogger<EmbeddingsController> logger)
        {
            _embeddingService = embeddingService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<EmbeddingResponse>> GetEmbeddingAsync([FromBody] EmbeddingRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    return BadRequest("Text is required");
                }

                var embedding = await _embeddingService.GetEmbeddingsAsync(request.Text, request.ModelName);

                return Ok(new EmbeddingResponse
                {
                    Embedding = embedding,
                    Dimensions = embedding.Length,
                    ModelName = request.ModelName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding");
                return StatusCode(500, "An error occurred while generating the embedding");
            }
        }

        [HttpPost("similarity")]
        public async Task<ActionResult<List<float>>> FindSimilarEmbeddingsAsync([FromBody] EmbeddingResponse request)
        {
            try
            {
                if (request.Embedding == null || request.Embedding.Length == 0)
                {
                    return BadRequest("Embedding is required");
                }

                var similarItems = await _embeddingService.FindSimilarEmbeddingsAsync(
                    request.Embedding,
                    limit: 5,
                    minScore: 0.7f
                );

                return Ok(similarItems.Select(item => item.Score).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar embeddings");
                return StatusCode(500, "An error occurred while finding similar embeddings");
            }
        }
    }
}
