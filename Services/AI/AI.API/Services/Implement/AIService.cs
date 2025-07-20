
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.Domain.Enums;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Document = AI.Domain.Models.Document;

namespace AI.API.Services.Implement
{
    public class AIService : IAIService
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
        private readonly IConfigurationService _configService;
        private readonly IPromptTemplateService _promptService;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<AIService> _logger;
        private readonly IUnitOfWork<DocAIDbContext> _unitOfWork;
        public AIService(
         Kernel kernel,
         IConfigurationService configService,
         IPromptTemplateService promptService,
         IMetricsService metricsService,
         IUnitOfWork<DocAIDbContext> unitOfWork,
         ILogger<AIService> logger)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
                _embeddingService = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize AI services from kernel");
                throw new InvalidOperationException("AI services not properly configured", ex);
            }
        }
        public async Task<AIResponse> GenerateAnswerAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            AIRequestLog requestLog = null;

            try
            {
                _logger.LogInformation("Starting AI generation for request {RequestId}", requestId);

                // Validate request
                await ValidateRequestAsync(request);

                // Log request
                var shouldLog = await _configService.GetConfigurationAsync("AI:EnableRequestLogging", true);
                if (shouldLog)
                {
                    requestLog = await LogRequestStartAsync(requestId, request);
                }

                // Get model configuration
                var modelConfig = await _configService.GetActiveModelConfigurationAsync(ModelType.Chat.ToString());
                if (modelConfig == null)
                {
                    throw new InvalidOperationException("No active chat model configuration found");
                }

                // Build prompt
                var prompt = await BuildPromptAsync(request);

                // Create execution settings
                var settings = CreateExecutionSettings(request, modelConfig);

                // Generate response
                var chatHistory = new ChatHistory();
                chatHistory.AddUserMessage(prompt);

                var result = await _chatCompletionService.GetChatMessageContentAsync(
                    chatHistory,
                    settings,
                    kernel: _kernel,
                    cancellationToken: cancellationToken);

                stopwatch.Stop();

                // Extract token count
                var tokensUsed = ExtractTokenCount(result.Metadata);

                // Log metrics
                await LogMetricsAsync(requestId, request.UserId, ModelType.Chat, tokensUsed,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Completed, null);

                // Update request log
                if (requestLog != null)
                {
                    await LogRequestCompleteAsync(requestLog, result.Content, RequestStatus.Completed);
                }

                return new AIResponse
                {
                    Success = true,
                    RequestId = requestId,
                    Answer = result.Content,
                    SourceDocuments = request.Documents,
                    TokensUsed = tokensUsed,
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    Metadata = new Dictionary<string, object>
                    {
                        ["model"] = modelConfig.ModelName,
                        ["provider"] = modelConfig.ProviderName
                    }
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Request {RequestId} was cancelled", requestId);
                await LogMetricsAsync(requestId, request?.UserId, ModelType.Chat, 0,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Failed, "Cancelled");

                if (requestLog != null)
                {
                    await LogRequestCompleteAsync(requestLog, null, RequestStatus.Failed);
                }

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response for request {RequestId}", requestId);
                await LogMetricsAsync(requestId, request?.UserId, ModelType.Chat, 0,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Failed, ex.Message);

                if (requestLog != null)
                {
                    await LogRequestCompleteAsync(requestLog, null, RequestStatus.Failed);
                }

                return new AIResponse
                {
                    Success = false,
                    RequestId = requestId,
                    Message = "An error occurred while generating the response",
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["type"] = ex.GetType().Name
                    }
                };
            }
        }
        public async IAsyncEnumerable<StreamChunk> StreamGenerateAnswerAsync(
                   AIRequest request,
                   [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
        //    var requestId = Guid.NewGuid().ToString();
        //    var stopwatch = Stopwatch.StartNew();
        //    var totalTokens = 0;
        //    var fullResponse = new StringBuilder();
        //    AIRequestLog requestLog = null;

        //    _logger.LogInformation("Starting streaming AI generation for request {RequestId}", requestId);

        //    try
        //    {
        //        // Validate request
        //        await ValidateRequestAsync(request);

        //        // Log request
        //        var shouldLog = await _configService.GetConfigurationAsync("AI:EnableRequestLogging", true);
        //        if (shouldLog)
        //        {
        //            requestLog = await LogRequestStartAsync(requestId, request);
        //        }

        //        // Get model configuration
        //        var modelConfig = await _configService.GetActiveModelConfigurationAsync(ModelType.Chat.ToString());
        //        if (modelConfig == null)
        //        {
        //            yield return new StreamChunk
        //            {
        //                Content = "Error: No active chat model configuration found",
        //                IsComplete = true
        //            };
        //            yield break;
        //        }

        //        // Build prompt
        //        var prompt = await BuildPromptAsync(request);

        //        // Create execution settings
        //        var settings = CreateExecutionSettings(request, modelConfig);
        //        settings.ExtensionData ??= new Dictionary<string, object>();
        //        settings.ExtensionData["stream"] = true;

        //        // Generate streaming response
        //        var chatHistory = new ChatHistory();
        //        chatHistory.AddUserMessage(prompt);

        //        await foreach (var chunk in _chatCompletionService.GetStreamingChatMessageContentsAsync(
        //            chatHistory,
        //            settings,
        //            kernel: _kernel,
        //            cancellationToken: cancellationToken))
        //        {
        //            if (chunk?.Content != null)
        //            {
        //                fullResponse.Append(chunk.Content);
        //                var tokenCount = EstimateTokens(chunk.Content);
        //                totalTokens += tokenCount;

        //                yield return new StreamChunk
        //                {
        //                    Content = chunk.Content,
        //                    IsComplete = false,
        //                    TokenCount = tokenCount
        //                };
        //            }
        //        }

        //        stopwatch.Stop();

        //        // Log final metrics
        //        await LogMetricsAsync(requestId, request.UserId, ModelType.Chat, totalTokens,
        //            stopwatch.ElapsedMilliseconds, RequestStatus.Completed, null);

        //        // Update request log
        //        if (requestLog != null)
        //        {
        //            await LogRequestCompleteAsync(requestLog, fullResponse.ToString(), RequestStatus.Completed);
        //        }

        //        // Send completion chunk
                yield return new StreamChunk
                {
                    Content = "",
                    IsComplete = true,
                    //TokenCount = totalTokens 
                };
          // }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.LogWarning("Streaming request {RequestId} was cancelled", requestId);
        //        await LogMetricsAsync(requestId, request?.UserId, ModelType.Chat, totalTokens,
        //            stopwatch.ElapsedMilliseconds, RequestStatus.Failed, "Cancelled");

        //        if (requestLog != null)
        //        {
        //            await LogRequestCompleteAsync(requestLog, fullResponse.ToString(), RequestStatus.Failed);
        //        }

        //        yield return new StreamChunk
        //        {
        //            Content = "\n[Stream cancelled]",
        //            IsComplete = true
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error in streaming generation for request {RequestId}", requestId);
        //        await LogMetricsAsync(requestId, request?.UserId, ModelType.Chat, totalTokens,
        //            stopwatch.ElapsedMilliseconds, RequestStatus.Failed, ex.Message);

        //        if (requestLog != null)
        //        {
        //            await LogRequestCompleteAsync(requestLog, fullResponse.ToString(), RequestStatus.Failed);
        //        }

        //        yield return new StreamChunk
        //        {
        //            Content = $"\n[Error: {ex.Message}]",
        //            IsComplete = true
        //        };
        //    }
        }

        public async Task<EmbeddingResponse> GenerateEmbeddingAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Generating embedding for document {DocumentId}", request.DocumentId);

                // Get model configuration
                var modelConfig = await _configService.GetActiveModelConfigurationAsync(ModelType.Embedding.ToString());
                if (modelConfig == null)
                {
                    throw new InvalidOperationException("No active embedding model configuration found");
                }

                // Prepare text
                var textToEmbed = PrepareTextForEmbedding(request);

                // Generate embedding
                var result = await _embeddingService.GenerateAsync(
                    textToEmbed,
                    cancellationToken: cancellationToken);

                stopwatch.Stop();

                // Log metrics
                var estimatedTokens = EstimateTokens(textToEmbed);
                await LogMetricsAsync(requestId, "system", ModelType.Embedding, estimatedTokens,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Completed, null);

                return new EmbeddingResponse
                {
                    Success = true,
                    RequestId = requestId,
                    DocumentId = request.DocumentId,
                    Embedding = result.Vector.ToArray(),
                    Dimensions = result.Vector.Length
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for document {DocumentId}", request.DocumentId);
                await LogMetricsAsync(requestId, "system", ModelType.Embedding, 0,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Failed, ex.Message);

                return new EmbeddingResponse
                {
                    Success = false,
                    RequestId = requestId,
                    DocumentId = request.DocumentId,
                    Message = $"Failed to generate embedding: {ex.Message}"
                };
            }
        }

        public async Task<BatchEmbeddingResponse> GenerateEmbeddingsBatchAsync(
            BatchEmbeddingRequest request,
            CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var results = new List<EmbeddingResult>();

            _logger.LogInformation("Starting batch embedding for {Count} documents", request.Documents.Count);

            try
            {
                // Process with controlled concurrency
                var maxConcurrency = await _configService.GetConfigurationAsync("AI:MaxEmbeddingConcurrency", 5);
                var semaphore = new SemaphoreSlim(maxConcurrency);

                var tasks = request.Documents.Select(async doc =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var response = await GenerateEmbeddingAsync(doc, cancellationToken);
                        return new EmbeddingResult
                        {
                            DocumentId = doc.DocumentId,
                            Success = response.Success,
                            Embedding = response.Embedding,
                            Dimensions = response.Dimensions,
                            ErrorMessage = response.Message
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing document {DocumentId} in batch", doc.DocumentId);
                        return new EmbeddingResult
                        {
                            DocumentId = doc.DocumentId,
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                results.AddRange(await Task.WhenAll(tasks));
                stopwatch.Stop();

                var successCount = results.Count(r => r.Success);
                var failureCount = results.Count(r => !r.Success);

                return new BatchEmbeddingResponse
                {
                    Success = true,
                    RequestId = requestId,
                    Results = results,
                    TotalProcessed = results.Count,
                    SuccessCount = successCount,
                    FailureCount = failureCount,
                    TotalTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in batch embedding processing");

                return new BatchEmbeddingResponse
                {
                    Success = false,
                    RequestId = requestId,
                    Message = $"Batch processing failed: {ex.Message}",
                    TotalProcessed = results.Count,
                    SuccessCount = results.Count(r => r.Success),
                    FailureCount = results.Count(r => !r.Success),
                    TotalTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }

        public async Task<bool> ValidateModelAvailabilityAsync(string modelType, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = await _configService.GetActiveModelConfigurationAsync(modelType);
                if (config == null) return false;

                // Try a simple test based on model type
                if (Enum.TryParse<ModelType>(modelType, out var type))
                {
                    switch (type)
                    {
                        case ModelType.Chat:
                            var chatHistory = new ChatHistory();
                            chatHistory.AddUserMessage("test");
                            var settings = new PromptExecutionSettings { ExtensionData = new Dictionary<string, object> { ["max_tokens"] = 1 } };
                            await _chatCompletionService.GetChatMessageContentAsync(chatHistory, settings, cancellationToken: cancellationToken);
                            return true;

                        case ModelType.Embedding:
                            await _embeddingService.GenerateAsync("test", cancellationToken: cancellationToken);
                            return true;

                        default:
                            return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Model validation failed for type {ModelType}", modelType);
                return false;
            }
        }

        #region Private Methods

        private async Task ValidateRequestAsync(AIRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.Question))
                throw new ArgumentException("Question cannot be empty");

            // Validate token limits
            var maxAllowedTokens = await _configService.GetConfigurationAsync("AI:MaxAllowedTokens", 4096);
            if (request.MaxTokens > maxAllowedTokens)
            {
                throw new ArgumentException($"MaxTokens cannot exceed {maxAllowedTokens}");
            }

            // Validate documents
            if (request.Documents?.Count > 0)
            {
                var maxDocuments = await _configService.GetConfigurationAsync("AI:MaxDocumentsPerRequest", 10);
                if (request.Documents.Count > maxDocuments)
                {
                    throw new ArgumentException($"Cannot process more than {maxDocuments} documents per request");
                }

                // Validate each document
                await ValidateDocument(request.Documents);
            }
        }

        private async Task<string> BuildPromptAsync(AIRequest request)
        {
            try
            {
                // Get template name from metadata or use default
                var templateName = request.Metadata?.ContainsKey("template") == true
                    ? request.Metadata["template"].ToString()
                    : "DefaultRAG";

                // Prepare variables
                var variables = new Dictionary<string, string>
                {
                    ["question"] = request.Question,
                    ["documents"] = FormatDocuments(request.Documents)
                };

                if (!string.IsNullOrEmpty(request.SystemPrompt))
                {
                    variables["system_prompt"] = request.SystemPrompt;
                }

                // Add any additional variables from metadata
                if (request.Metadata != null)
                {
                    foreach (var kvp in request.Metadata.Where(m => m.Key.StartsWith("var_")))
                    {
                        variables[kvp.Key.Substring(4)] = kvp.Value?.ToString() ?? "";
                    }
                }

                return await _promptService.RenderTemplateAsync(templateName, variables);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to render template, using fallback prompt");

                // Fallback to simple prompt
                var prompt = new StringBuilder();

                if (!string.IsNullOrEmpty(request.SystemPrompt))
                {
                    prompt.AppendLine(request.SystemPrompt);
                    prompt.AppendLine();
                }

                if (request.Documents?.Count > 0)
                {
                    prompt.AppendLine("Context:");
                    prompt.AppendLine(FormatDocuments(request.Documents));
                    prompt.AppendLine();
                }

                prompt.AppendLine($"Question: {request.Question}");
                prompt.AppendLine("Answer:");

                return prompt.ToString();
            }
        }
        private string FormatDocuments(List<Document> documents)
        {
            if (documents == null || documents.Count == 0)
                return "No documents provided.";

            var sb = new StringBuilder();

            foreach (var doc in documents.OrderByDescending(d => d.RelevanceScore ?? 0))
            {
                sb.AppendLine($"[Document: {doc.Title}]");
                sb.AppendLine($"Document ID: {doc.DocumentId}");
                sb.AppendLine($"Version: {doc.VersionCode}");

                if (!string.IsNullOrEmpty(doc.TypeName))
                    sb.AppendLine($"Type: {doc.TypeName}");

                if (!string.IsNullOrEmpty(doc.Summary))
                    sb.AppendLine($"Summary: {doc.Summary}");

                if (doc.EffectiveFrom.HasValue || doc.EffectiveUntil.HasValue)
                {
                    var effectivePeriod = $"Effective: {doc.EffectiveFrom?.ToString("yyyy-MM-dd") ?? "N/A"} to {doc.EffectiveUntil?.ToString("yyyy-MM-dd") ?? "current"}";
                    sb.AppendLine(effectivePeriod);
                }

                if (!string.IsNullOrEmpty(doc.SignedBy))
                    sb.AppendLine($"Signed by: {doc.SignedBy}");

                if (doc.RelevanceScore.HasValue)
                    sb.AppendLine($"Relevance: {doc.RelevanceScore:P0}");

                sb.AppendLine($"Content: {doc.Content}");
                sb.AppendLine("---");
            }

            return sb.ToString();
        }

        private string PrepareTextForEmbedding(EmbeddingRequest request)
        {
            var parts = new List<string>();

            // Add document type if available
            if (!string.IsNullOrEmpty(request.TypeName))
            {
                parts.Add($"Document Type: {request.TypeName}");
            }

            // Add title
            if (!string.IsNullOrEmpty(request.Title))
            {
                parts.Add($"Title: {request.Title}");
            }

            // Add summary
            if (!string.IsNullOrEmpty(request.Summary))
            {
                parts.Add($"Summary: {request.Summary}");
            }

            // Add department ID as context
            if (request.DepartmentId.HasValue)
            {
                parts.Add($"Department: {request.DepartmentId}");
            }

            // Main content
            parts.Add("Content:");
            parts.Add(request.Content);

            // Add structured metadata
            if (request.Metadata != null && request.Metadata.Count > 0)
            {
                parts.Add("Metadata:");
                foreach (var kvp in request.Metadata)
                {
                    parts.Add($"{kvp.Key}: {kvp.Value}");
                }
            }

            return string.Join("\n", parts);
        }

        private PromptExecutionSettings CreateExecutionSettings(AIRequest request, ModelConfigurationResponse modelConfig)
        {
            var settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = request.MaxTokens > 0 ? request.MaxTokens : modelConfig.MaxTokens,
                    ["temperature"] = request.Temperature > 0 ? request.Temperature : modelConfig.Temperature,
                    ["top_p"] = request.TopP > 0 ? request.TopP : modelConfig.TopP
                }
            };

            // Add any additional settings from metadata
            if (request.Metadata != null)
            {
                foreach (var kvp in request.Metadata.Where(m => m.Key.StartsWith("setting_")))
                {
                    settings.ExtensionData[kvp.Key.Substring(8)] = kvp.Value;
                }
            }

            return settings;
        }

        private int ExtractTokenCount(IReadOnlyDictionary<string, object> metadata)
        {
            if (metadata == null) return 0;

            try
            {
                // Try different possible paths for token count
                if (metadata.TryGetValue("usage", out var usage))
                {
                    if (usage is Dictionary<string, object> usageDict)
                    {
                        if (usageDict.TryGetValue("total_tokens", out var totalTokens))
                        {
                            return Convert.ToInt32(totalTokens);
                        }
                        if (usageDict.TryGetValue("completion_tokens", out var completionTokens))
                        {
                            return Convert.ToInt32(completionTokens);
                        }
                    }
                    else if (usage is JsonElement jsonElement)
                    {
                        if (jsonElement.TryGetProperty("total_tokens", out var totalTokensProp))
                        {
                            return totalTokensProp.GetInt32();
                        }
                    }
                }

                // Fallback to estimation
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to extract token count from metadata");
                return 0;
            }
        }

        private int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            // More accurate estimation based on common patterns
            // Average ~1 token per 4 characters for English
            // Adjust for other languages if needed
            return (int)(text.Length / 3.5);
        }

        private async Task<AIRequestLog> LogRequestStartAsync(string requestId, AIRequest request)
        {
            try
            {
                var documentInfo = request.Documents?.Select(d => new
                {
                    documentId = d.DocumentId,
                    versionId = d.VersionId,
                    title = d.Title,
                    type = d.TypeName,
                    departmentId = d.DepartmentId,
                    isPublic = d.IsPublic
                }).ToList();

                var requestLog = new AIRequestLog
                {
                    RequestId = requestId,
                    UserId = request.UserId ?? "anonymous",
                    ModelType = ModelType.Chat,
                    RequestContent = JsonSerializer.Serialize(new
                    {
                        question = request.Question,
                        documents = documentInfo,
                        documentCount = request.Documents?.Count ?? 0,
                        maxTokens = request.MaxTokens,
                        temperature = request.Temperature,
                        topP = request.TopP,
                        metadata = request.Metadata
                    }),
                    Status = RequestStatus.Processing,
                    CreatedAt = DateTime.UtcNow
                };

                var repo = _unitOfWork.GetRepository<AIRequestLog>();
                await repo.InsertAsync(requestLog);
                await _unitOfWork.CommitAsync();

                return requestLog;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log request start");
                return null;
            }
        }

        private async Task LogRequestCompleteAsync(AIRequestLog requestLog, string response, RequestStatus status)
        {
            try
            {
                requestLog.ResponseContent = response != null
                    ? JsonSerializer.Serialize(new { answer = response.Length > 1000 ? response.Substring(0, 1000) + "..." : response })
                    : null;
                requestLog.Status = status;
                requestLog.CompletedAt = DateTime.UtcNow;

                var repo = _unitOfWork.GetRepository<AIRequestLog>();
                repo.UpdateAsync(requestLog);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log request completion");
            }
        }

        private async Task LogMetricsAsync(
            string requestId,
            string userId,
            ModelType modelType,
            int tokensUsed,
            long responseTimeMs,
            RequestStatus status,
            string errorMessage)
        {
            await _metricsService.LogUsageAsync(new UsageMetric
            {
                RequestId = requestId,
                UserId = userId ?? "anonymous",
                ModelType = modelType,
                TokensUsed = tokensUsed,
                ResponseTimeMs = (int)responseTimeMs,
                Status = status,
                ErrorMessage = errorMessage,
                CreatedAt = DateTime.UtcNow
            });
        }
        private async Task ValidateDocument(List<Document> documents)
        {
            if (documents == null || documents.Count == 0)
                return;

            foreach (var doc in documents)
            {
                // Validate required fields
                if (string.IsNullOrEmpty(doc.DocumentId))
                    throw new ArgumentException($"Document ID is required for all documents");

                if (string.IsNullOrEmpty(doc.Content))
                    throw new ArgumentException($"Content is required for document {doc.DocumentId}");

                // Validate effective dates
                if (doc.EffectiveFrom.HasValue && doc.EffectiveUntil.HasValue)
                {
                    if (doc.EffectiveFrom > doc.EffectiveUntil)
                    {
                        throw new ArgumentException($"Invalid effective dates for document {doc.DocumentId}");
                    }

                    // Check if document is currently effective
                    var now = DateTime.UtcNow;
                    if (doc.EffectiveUntil < now)
                    {
                        _logger.LogWarning("Document {DocumentId} has expired (effective until {EffectiveUntil})",
                            doc.DocumentId, doc.EffectiveUntil);
                    }
                }

                // Validate file info if provided
                if (!string.IsNullOrEmpty(doc.FilePath))
                {
                    if (string.IsNullOrEmpty(doc.FileType))
                    {
                        _logger.LogWarning("Document {DocumentId} has file path but no file type", doc.DocumentId);
                    }
                }
            }
        }
    #endregion
}
}