using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.TextGeneration;
using Document = AI.Domain.Models.Document;

namespace AI.API.Services.Implement
{
    public class AIService : IAIService
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
        private readonly ILogger<AIService> _logger;
        private readonly IConfiguration _configuration;
        public AIService(
       Kernel kernel,
       IConfiguration configuration,
       ILogger<AIService> logger)
        {
            _kernel = kernel;
            _configuration = configuration;
            _logger = logger;
            _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            _embeddingService = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        }
        public async Task<AIResponse> GenerateAnswerAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation("Starting RAG answer generation for request {RequestId}", requestId);

                // Build RAG prompt from documents
                var ragPrompt = BuildRAGPrompt(request.SystemPrompt, request.Documents, request.Question);

                // Apply admin-configured settings or use request settings
                var executionSettings = CreateExecutionSettings(request);

                var result = await _chatCompletionService.GetChatMessageContentAsync(
                    ragPrompt,
                    executionSettings,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("RAG answer generation completed for request {RequestId}", requestId);

                return new AIResponse
                {
                    Answer = result.Content,
                    SourceDocuments = request.Documents,
                    TokensUsed = ExtractTokenCount(result.Metadata),
                    RequestId = requestId,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating RAG answer for request {RequestId}", requestId);
                return new AIResponse
                {
                    RequestId = requestId,
                    Success = false,
                    Error = ex.Message
                };
            }
        }
        public async IAsyncEnumerable<string> StreamGenerateAnswerAsync(AIRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting streaming RAG answer generation for request {RequestId}", requestId);

            // Cách 1: Tách setup ra khỏi yield context
            string? ragPrompt = null;
            PromptExecutionSettings? executionSettings = null;
            string? errorMessage = null;

            try
            {
                ragPrompt = BuildRAGPrompt(request.SystemPrompt, request.Documents, request.Question);
                executionSettings = CreateExecutionSettings(request, streaming: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming RAG answer generation for request {RequestId}", requestId);
                errorMessage = $"Error: {ex.Message}";
            }

            // Nếu có lỗi setup, yield error và dừng
            if (errorMessage != null)
            {
                yield return errorMessage;
                yield break;
            }

            // Streaming phase - không có try-catch xung quanh yield
            await foreach (var content in _chatCompletionService.GetStreamingChatMessageContentsAsync(
                ragPrompt!,
                executionSettings!,
                cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(content.Content))
                {
                    yield return content.Content;
                }
            }

            _logger.LogInformation("Streaming RAG answer generation completed for request {RequestId}", requestId);
        }
        private PromptExecutionSettings CreateExecutionSettings(AIRequest request, bool streaming = false)
        {
            // Get admin-configured defaults
            var defaultMaxTokens = _configuration.GetValue<int>("AI:DefaultMaxTokens", 2048);
            var defaultTemperature = _configuration.GetValue<double>("AI:DefaultTemperature", 0.7);
            var defaultTopP = _configuration.GetValue<double>("AI:DefaultTopP", 0.9);

            var settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = request.MaxTokens > 0 ? request.MaxTokens : defaultMaxTokens,
                    ["temperature"] = request.Temperature > 0 ? request.Temperature : defaultTemperature,
                    ["top_p"] = request.TopP > 0 ? request.TopP : defaultTopP
                }
            };

            if (streaming)
            {
                settings.ExtensionData["stream"] = true;
            }

            return settings;
        }
        public async Task<EmbeddingResponse> GenerateEmbeddingAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation("Generating embedding for document {DocumentId}", request.DocumentId);

                // Prepare text for embedding
                var textToEmbed = PrepareTextForEmbedding(request);

                var result = await _embeddingService.GenerateAsync(textToEmbed, cancellationToken: cancellationToken);

                _logger.LogInformation("Embedding generation completed for document {DocumentId}", request.DocumentId);

                return new EmbeddingResponse
                {
                    DocumentId = request.DocumentId,
                    Embedding = result.Vector.ToArray(),
                    Dimensions = result.Vector.Length,
                    RequestId = requestId,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for document {DocumentId}", request.DocumentId);
                return new EmbeddingResponse
                {
                    DocumentId = request.DocumentId,
                    RequestId = requestId,
                    Success = false,
                    Error = ex.Message
                };
            }
        }
        public async Task<BatchEmbeddingResponse> GenerateEmbeddingsBatchAsync(BatchEmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString();

            _logger.LogInformation("Generating embeddings for {Count} documents in batch {RequestId}",
                request.Documents.Count, requestId);

            var results = new List<EmbeddingResponse>();
            var successCount = 0;
            var failureCount = 0;

            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
            var tasks = request.Documents.Select(async doc =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await GenerateEmbeddingAsync(doc, cancellationToken);
                    if (result.Success) successCount++;
                    else failureCount++;
                    return result;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            results.AddRange(await Task.WhenAll(tasks));

            _logger.LogInformation("Batch embedding completed for {RequestId}. Success: {Success}, Failure: {Failure}",
                requestId, successCount, failureCount);

            return new BatchEmbeddingResponse
            {
                Results = results,
                TotalProcessed = results.Count,
                SuccessCount = successCount,
                FailureCount = failureCount,
                RequestId = requestId
            };
        }
        private int ExtractTokenCount(IReadOnlyDictionary<string, object>? metadata)
        {
            if (metadata == null) return 0;

            try
            {
                if (metadata.TryGetValue("usage", out var usage) && usage is Dictionary<string, object> usageDict)
                {
                    if (usageDict.TryGetValue("total_tokens", out var totalTokens))
                    {
                        return Convert.ToInt32(totalTokens);
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return 0;
        }
        private string BuildRAGPrompt(string systemPrompt, List<Document> documents, string question)
        {
            var prompt = new StringBuilder();

            // System prompt
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                prompt.AppendLine(systemPrompt);
            }

            // Default RAG instructions
            prompt.AppendLine("\nInstructions:");
            prompt.AppendLine("- Answer based ONLY on the provided documents");
            prompt.AppendLine("- If information is not in the documents, say 'I cannot find this information in the provided documents'");
            prompt.AppendLine("- Always cite the source document(s) used");
            prompt.AppendLine("- Be accurate and concise");

            // Add context from documents
            if (documents.Any())
            {
                prompt.AppendLine("\n=== CONTEXT DOCUMENTS ===");

                foreach (var doc in documents.OrderByDescending(d => d.DocumentName))
                {
                    prompt.AppendLine($"\n[Document: {doc.Title}]");
                    prompt.AppendLine($"Content: {doc.Content}");
                }

                prompt.AppendLine("\n=== END CONTEXT ===");
            }

            // User question
            prompt.AppendLine($"\nQuestion: {question}");
            prompt.AppendLine("\nAnswer:");

            return prompt.ToString();
        }
        private string PrepareTextForEmbedding(EmbeddingRequest request)
        {
            var textBuilder = new StringBuilder();

            // Include title if provided
            if (!string.IsNullOrEmpty(request.Title))
            {
                textBuilder.AppendLine(request.Title);
            }

            // Main content
            textBuilder.AppendLine(request.Content);

            // Include relevant metadata as text
            if (request.Metadata != null)
            {
                foreach (var meta in request.Metadata)
                {
                    if (meta.Key.ToLower() is "category" or "department" or "type")
                    {
                        textBuilder.AppendLine($"{meta.Key}: {meta.Value}");
                    }
                }
            }

            return textBuilder.ToString();
        }
    }
}
