using System.Text;
using AI.API.Constants;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.KernelMemory.AI;

namespace AI.API.Controllers
{
    [ApiController]
    [Route(ApiEndPointConstant.ApiEndpoint)]
    [Authorize] 
    public class AIController : ControllerBase
    {
        private readonly ITextGenerator _textGenerator; // REVIEW POINT: Inject ITextGenerator của KM
        private readonly ITextEmbeddingGenerator _embeddingGenerator; // REVIEW POINT: Inject ITextEmbeddingGenerator của KM
        private readonly ILogger<AIController> _logger;

        public AIController(
                          ILogger<AIController> logger,
                          ITextGenerator textGenerator,
                          ITextEmbeddingGenerator embeddingGenerator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _textGenerator = textGenerator ?? throw new ArgumentNullException(nameof(textGenerator));
            _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        }

        [HttpPost("generate")] // Endpoint cho Chat Completion
        [ProducesResponseType(typeof(AIResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IAsyncEnumerable<string>), StatusCodes.Status200OK)]
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
                // Xây dựng prompt tương tự như ChatService sẽ làm
                // AI Microservice này chỉ đóng vai trò là connector,
                // nên nó mong đợi prompt đã được xây dựng sẵn từ Chat Service.
                // Tuy nhiên, để API này có thể test độc lập hoặc dùng cho mục đích khác,
                // chúng ta vẫn xây dựng prompt tại đây nếu Documents được cung cấp.
                var promptBuilder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                {
                    promptBuilder.AppendLine(request.SystemPrompt);
                }
                promptBuilder.AppendLine("User's question: " + request.Question);
                if (request.Documents != null && request.Documents.Any())
                {
                    promptBuilder.AppendLine("\n--- Relevant Documents ---");
                    foreach (var doc in request.Documents.OrderBy(d => d.DocumentName).ThenBy(d => d.ChunkId))
                    {
                        promptBuilder.AppendLine($"Document: {doc.DocumentName ?? "N/A"} (Title: {doc.Title ?? "N/A"})");
                        promptBuilder.AppendLine($"Chunk ID: {doc.ChunkId}");
                        promptBuilder.AppendLine($"Content:\n{doc.Content}");
                        promptBuilder.AppendLine("---");
                    }
                    promptBuilder.AppendLine("--- End of Relevant Documents ---");
                }
                var fullPrompt = promptBuilder.ToString();

                // Lấy các tùy chọn từ IConfiguration
                var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>(); // Lấy IConfiguration qua ServiceProvider
                var textGenerationOptions = new TextGenerationOptions // Options của KM
                {
                    Temperature = config.GetValue<double>("Ollama:Temperature", 0.7),
                    NucleusSampling = config.GetValue<double>("Ollama:TopP", 0.9), // NucleusSampling tương ứng với TopP
                    MaxTokens = config.GetValue<int?>("Ollama:NumPredict", 1024) // MaxTokens tương ứng với NumPredict
                };

                if (request.StreamResponse)
                {
                    _logger.LogInformation("Streaming response requested for AI generation.");
                    // REVIEW POINT: Gọi GenerateTextAsync của ITextGenerator
                    return Ok(StreamGeneratedText(fullPrompt, textGenerationOptions));
                }
                else
                {
                    _logger.LogInformation("Non-streaming response requested for AI generation.");
                    // REVIEW POINT: Gọi GenerateTextAsync của ITextGenerator và nối chuỗi
                    var responseBuilder = new StringBuilder();
                    await foreach (var chunk in _textGenerator.GenerateTextAsync(fullPrompt, textGenerationOptions))
                    {
                        responseBuilder.Append(chunk.Text);
                    }
                    // Tên model có thể lấy từ IConfiguration
                    var modelUsed = config.GetValue<string>("Ollama:TextGenerationModel") ?? "unknown";
                    return Ok(new AIResponse
                    {
                        Answer = responseBuilder.ToString().Trim(),
                        ModelUsed = modelUsed
                    });
                }
            }
            catch (ApplicationException appEx)
            {
                _logger.LogError(appEx, "Application error during AI generation.");
                return Problem(detail: appEx.Message, statusCode: StatusCodes.Status500InternalServerError, title: "AI Generation Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during AI generation.");
                return Problem(detail: "An unexpected error occurred. Please try again later.", statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected Server Error");
            }
        }
        // REVIEW POINT: Thêm Endpoint mới cho Embedding Generation
        private async IAsyncEnumerable<string> StreamGeneratedText(string prompt, TextGenerationOptions options)
        {
            await foreach (var chunk in _textGenerator.GenerateTextAsync(prompt, options))
            {
                if (chunk?.Text != null)
                {
                    yield return chunk.Text;
                }
            }
        }

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
                // REVIEW POINT: Gọi GenerateEmbeddingAsync của ITextEmbeddingGenerator
                var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(request.Text);

                var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var modelUsed = config.GetValue<string>("Ollama:EmbeddingModel") ?? "unknown";
                return Ok(new EmbeddingResponse
                {
                    Embedding = embedding.Data.ToArray().ToList(), // Chuyển ReadOnlyMemory<float> sang List<float>
                    ModelUsed = modelUsed // Lấy tên model từ IConfiguration
                });
            }
            catch (ArgumentException argEx)
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
