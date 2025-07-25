
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.API.Services;
using AI.Domain.Enums;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextGeneration;
using Microsoft.SemanticKernel.Embeddings;
using Document = AI.Domain.Models.Document;
using Microsoft.AspNetCore.Http;
using OllamaSharp.Models;

namespace AI.API.Services.Implement
{
    public class AIService : IAIService
    {
        private readonly ITextGenerationService _defaultTextService;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
        private readonly IAIConfigurationService _configService;
        private readonly IDynamicProviderService _dynamicProviderService;
        private readonly IKernelProviderService _kernelProviderService;
        private readonly IMetricsService _metricsService;
        private readonly IUnitOfWork<DocAIDbContext> _unitOfWork;
        private readonly ILogger<AIService> _logger;

        // Removed unused caching fields - using database-driven model selection instead
        private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);

        private const string VIETNAMESE_SYSTEM_PROMPT = @"Bạn là một trợ lý AI thông minh và hữu ích. 
Hãy luôn trả lời bằng tiếng Việt một cách tự nhiên, lịch sự và chính xác. 
Nếu câu hỏi bằng tiếng Anh hoặc ngôn ngữ khác, hãy hiểu và trả lời bằng tiếng Việt. 
Cung cấp thông tin chi tiết, hữu ích và dễ hiểu.";
        public AIService(
             ITextGenerationService defaultTextService,
             IEmbeddingGenerator<string, Embedding<float>> embeddingService,
             IAIConfigurationService configService,
             IDynamicProviderService dynamicProviderService,
             IKernelProviderService kernelProviderService,
             IMetricsService metricsService,
             IUnitOfWork<DocAIDbContext> unitOfWork,
             ILogger<AIService> logger)
        {
            _defaultTextService = defaultTextService ?? throw new ArgumentNullException(nameof(defaultTextService));
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _dynamicProviderService = dynamicProviderService ?? throw new ArgumentNullException(nameof(dynamicProviderService));
            _kernelProviderService = kernelProviderService ?? throw new ArgumentNullException(nameof(kernelProviderService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public async Task<AIResponse> GenerateAnswerAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = Stopwatch.StartNew();
            AIRequestLog requestLog = null;

            try
            {
                _logger.LogInformation("[{RequestId}] Starting text generation for user: {UserId}", requestId, request.UserId);


                var shouldLog = await _configService.GetConfigurationAsync("AI:EnableRequestLogging", true);
                if (shouldLog)
                {
                    requestLog = await LogRequestStartAsync(requestId, request, ModelType.Chat);
                }

                // Get text generation service (try dynamic kernel first, fallback to default)
                var textService = await GetTextGenerationServiceAsync();

                // Get current config for validation
                var aiConfig = await _configService.GetActiveAIModelAsync();

                // Validate request
                ValidateRequest(request, aiConfig);

                // Create execution settings
                var settings = CreateExecutionSettings(request, aiConfig);

                _logger.LogDebug("[{RequestId}] Using settings - MaxTokens: {MaxTokens}, Temperature: {Temperature}, TopP: {TopP}",
                    requestId, settings.ExtensionData?["max_tokens"], settings.ExtensionData?["temperature"], settings.ExtensionData?["top_p"]);

                var enhancedPrompt = PrepareVietnamesePrompt(request.Prompt);

                // Generate response
                var result = await textService.GetTextContentsAsync(
                              enhancedPrompt, settings, cancellationToken: cancellationToken);

                var content = result.FirstOrDefault()?.Text ?? "Xin lỗi, tôi không thể tạo phản hồi lúc này.";
                var tokensUsed = ExtractTokenCount(result.FirstOrDefault()?.Metadata) ?? EstimateTokens(content);

                stopwatch.Stop();

                // Log metrics
                await LogMetricsAsync(requestId, request.UserId, ModelType.Chat, tokensUsed,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Completed, null);

                if (requestLog != null)
                {
                    await LogRequestCompleteAsync(requestLog, content, RequestStatus.Completed, tokensUsed, (int)stopwatch.ElapsedMilliseconds);
                }

                _logger.LogInformation("[{RequestId}] Generation completed in {Ms}ms, {Tokens} tokens for user: {UserId}",
                    requestId, stopwatch.ElapsedMilliseconds, tokensUsed, request.UserId);

                return new AIResponse
                {
                    Success = true,
                    RequestId = requestId,
                    Content = content,
                    TokensUsed = tokensUsed,
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Generation failed for user: {UserId}", requestId, request.UserId);
                stopwatch.Stop();

                await LogMetricsAsync(requestId, request.UserId, ModelType.Chat, 0,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Failed, ex.Message);

                if (requestLog != null)
                {
                    await LogRequestCompleteAsync(requestLog, null, RequestStatus.Failed, 0, (int)stopwatch.ElapsedMilliseconds);
                }

                return new AIResponse
                {
                    Success = false,
                    RequestId = requestId,
                    Message = $"Generation failed: {ex.Message}",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }
        public async IAsyncEnumerable<StreamChunk> StreamGenerateAnswerAsync(
                  AIRequest request,
                  [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = Stopwatch.StartNew();
            var totalTokens = 0;
            var fullResponse = new StringBuilder();
            AIRequestLog requestLog = null;
            var chunks = new List<StreamChunk>();
            bool hasError = false;
            string errorMessage = null;

            _logger.LogInformation("[{RequestId}] Starting streaming generation for user: {UserId}", requestId, request.UserId);

            try
            {
                // Log request if enabled
                var shouldLog = await _configService.GetConfigurationAsync("AI:EnableRequestLogging", true);
                if (shouldLog)
                {
                    requestLog = await LogRequestStartAsync(requestId, request, ModelType.Chat);
                }

                // Get text generation service
                var textService = await GetTextGenerationServiceAsync();
                var aiConfig = await _configService.GetActiveAIModelAsync();

                // Validate request
                ValidateRequest(request, aiConfig);

                // Create execution settings
                var settings = CreateExecutionSettings(request, aiConfig);

                // Prepare prompt with Vietnamese instruction
                var enhancedPrompt = PrepareVietnamesePrompt(request.Prompt);

                // Generate streaming response
                await foreach (var streamContent in textService.GetStreamingTextContentsAsync(
                    enhancedPrompt, settings, cancellationToken: cancellationToken))
                {
                    if (!string.IsNullOrEmpty(streamContent.Text))
                    {
                        fullResponse.Append(streamContent.Text);
                        var tokenCount = EstimateTokens(streamContent.Text);
                        totalTokens += tokenCount;

                        chunks.Add(new StreamChunk
                        {
                            Content = streamContent.Text,
                            IsComplete = false,
                            TokenCount = tokenCount,
                            RequestId = requestId
                        });
                    }
                }

                // Add completion chunk
                chunks.Add(new StreamChunk
                {
                    Content = "",
                    IsComplete = true,
                    TokenCount = totalTokens,
                    RequestId = requestId
                });
            }
            catch (Exception ex)
            {
                hasError = true;
                errorMessage = ex.Message;
                chunks.Clear();
                chunks.Add(new StreamChunk
                {
                    Content = $"Lỗi streaming: {errorMessage}",
                    IsComplete = true,
                    RequestId = requestId
                });
                _logger.LogError(ex, "[{RequestId}] Streaming failed for user: {UserId}", requestId, request.UserId);
            }
            finally
            {
                stopwatch.Stop();
                await LogMetricsAsync(requestId, request.UserId, ModelType.Chat, totalTokens,
                    stopwatch.ElapsedMilliseconds, hasError ? RequestStatus.Failed : RequestStatus.Completed, errorMessage);

                if (requestLog != null)
                {
                    await LogRequestCompleteAsync(requestLog, fullResponse.ToString(),
                        hasError ? RequestStatus.Failed : RequestStatus.Completed, totalTokens, (int)stopwatch.ElapsedMilliseconds);
                }

                _logger.LogInformation("[{RequestId}] Streaming completed in {Ms}ms, {Tokens} tokens, Error: {HasError} for user: {UserId}",
                    requestId, stopwatch.ElapsedMilliseconds, totalTokens, hasError, request.UserId);
            }

            // Yield all chunks
            foreach (var chunk in chunks)
            {
                yield return chunk;
            }
        }

        public async Task<EmbeddingResponse> GenerateEmbeddingAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("[{RequestId}] Generating embedding for document: {DocumentId}", requestId, request.DocumentId ?? "N/A");

                // Validate request
                ValidateEmbeddingRequest(request);

                // Prepare text for embedding
                var textToEmbed = PrepareTextForEmbedding(request);

                // Generate embedding using fixed OpenAI service
                var result = await _embeddingService.GenerateAsync(new[] { textToEmbed }, null, cancellationToken);

                stopwatch.Stop();
                var tokensUsed = EstimateTokens(textToEmbed);

                // Log metrics
                await LogMetricsAsync(requestId, "EmbeddingService", ModelType.Embedding, tokensUsed,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Completed, null);

                var embedding = result.FirstOrDefault();
                var embeddingArray = embedding?.Vector.ToArray() ?? new float[0];

                _logger.LogInformation("[{RequestId}] Embedding generated in {Ms}ms, dimensions: {Dimensions}",
                    requestId, stopwatch.ElapsedMilliseconds, embeddingArray.Length);

                return new EmbeddingResponse
                {
                    Success = true,
                    RequestId = requestId,
                    DocumentId = request.DocumentId,
                    Embedding = embeddingArray,
                    Dimensions = embeddingArray.Length
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Embedding generation failed for document: {DocumentId}",
                    requestId, request.DocumentId ?? "N/A");
                stopwatch.Stop();

                await LogMetricsAsync(requestId, "EmbeddingService", ModelType.Embedding, 0,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Failed, ex.Message);

                return new EmbeddingResponse
                {
                    Success = false,
                    RequestId = requestId,
                    DocumentId = request.DocumentId,
                    Message = $"Tạo embedding thất bại: {ex.Message}"
                };
            }
        }

        public async Task<BatchEmbeddingResponse> GenerateEmbeddingsBatchAsync(BatchEmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = Stopwatch.StartNew();
            var results = new List<EmbeddingResult>();

            _logger.LogInformation("[{RequestId}] Starting batch embedding for {Count} items from {Source}",
                requestId, request.Items.Count, request.SourceService);

            try
            {
                // Validate batch request
                if (request.Items == null || request.Items.Count == 0)
                {
                    throw new ArgumentException("Không có item nào được cung cấp cho batch embedding");
                }

                var maxBatchSize = await _configService.GetConfigurationAsync("AI:MaxEmbeddingBatchSize", 100);
                if (request.Items.Count > maxBatchSize)
                {
                    throw new ArgumentException($"Kích thước batch {request.Items.Count} vượt quá giới hạn {maxBatchSize}");
                }

                // Process with controlled concurrency
                var maxConcurrency = await _configService.GetConfigurationAsync("AI:MaxEmbeddingConcurrency", 5);
                using var semaphore = new SemaphoreSlim(maxConcurrency);

                var tasks = request.Items.Select(async (item, index) =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var embeddingRequest = new EmbeddingRequest
                        {
                            Text = item.Text,
                            DocumentId = item.DocumentId,
                            VersionId = item.VersionId,
                            Title = item.Title,
                            Summary = item.Summary,
                            TypeName = item.TypeName,
                            DepartmentId = item.DepartmentId
                        };

                        var response = await GenerateEmbeddingAsync(embeddingRequest, cancellationToken);

                        return new EmbeddingResult
                        {
                            DocumentId = item.DocumentId ?? $"item_{index}",
                            Success = response.Success,
                            Embedding = response.Embedding,
                            Dimensions = response.Dimensions,
                            ErrorMessage = response.Message
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[{RequestId}] Error processing batch item {Index}: {DocumentId}",
                            requestId, index, item.DocumentId ?? "N/A");

                        return new EmbeddingResult
                        {
                            DocumentId = item.DocumentId ?? $"item_{index}",
                            Success = false,
                            ErrorMessage = $"Lỗi xử lý: {ex.Message}"
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

                _logger.LogInformation("[{RequestId}] Batch embedding completed in {Ms}ms. Success: {Success}, Failed: {Failed}",
                    requestId, stopwatch.ElapsedMilliseconds, successCount, failureCount);

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
                _logger.LogError(ex, "[{RequestId}] Critical error in batch embedding processing", requestId);
                stopwatch.Stop();

                return new BatchEmbeddingResponse
                {
                    Success = false,
                    RequestId = requestId,
                    Message = $"Xử lý batch thất bại: {ex.Message}",
                    Results = results,
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
                _logger.LogInformation("Validating model availability for type: {ModelType}", modelType);

                if (Enum.TryParse<ModelType>(modelType, out var type))
                {
                    switch (type)
                    {
                        case ModelType.Chat:
                            // Test dynamic text generation service
                            var textService = await GetTextGenerationServiceAsync();
                            var testPrompt = "Xin chào";
                            var settings = new PromptExecutionSettings
                            {
                                ExtensionData = new Dictionary<string, object> { ["max_tokens"] = 5 }
                            };
                            var result = await textService.GetTextContentsAsync(testPrompt, settings, cancellationToken: cancellationToken);
                            return result?.Any() == true;

                        case ModelType.Embedding:
                            // Test fixed OpenAI embedding service
                            var testResult = await _embeddingService.GenerateAsync(new[] { "test" }, null, cancellationToken);
                            return testResult != null && testResult.Any();

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

        private async Task<ITextGenerationService> GetTextGenerationServiceAsync()
        {
            try
            {
                // 1. Truy vấn danh sách model từ DB
                var modelRepository = _unitOfWork.GetRepository<AIModelConfiguration>();
                var modelConfigs = await modelRepository.GetListAsync(
                    predicate: m => m.IsEnabled == true,
                    orderBy: null,
                    include: null);

                // 2. Ưu tiên chọn IsActive = true và IsTestedSuccessfully = true
                var activeModel = modelConfigs.FirstOrDefault(m => m.IsActive && m.IsTestedSuccessfully);

                if (activeModel != null && !string.IsNullOrEmpty(activeModel.ApiKey))
                {
                    _logger.LogDebug("Using active model: {ModelName} ({ProviderType})", activeModel.Name, activeModel.ProviderType);

                    var textService = await _kernelProviderService.CreateTextGenerationServiceAsync(activeModel);

                    // Update last used time
                    activeModel.LastUsedAt = DateTime.UtcNow;
                    modelRepository.UpdateAsync(activeModel);
                    await _unitOfWork.CommitAsync();

                    return textService;
                }

                // 3. Fallback về HuggingFace từ appsettings.json (fallback duy nhất)
                _logger.LogDebug("No active model found, using HuggingFace fallback from appsettings");
                return _defaultTextService;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get text generation service, using default: {Error}", ex.Message);
                return _defaultTextService;
            }
        }

        private void ValidateRequest(AIRequest request, AIModelConfig aiConfig)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ArgumentException("Prompt không được để trống");

            if (string.IsNullOrWhiteSpace(request.UserId))
                throw new ArgumentException("UserId là bắt buộc");

            var maxTokens = aiConfig?.MaxTokens ?? 2048;
            if (request.MaxTokens.HasValue && request.MaxTokens.Value > maxTokens)
                throw new ArgumentException($"Max tokens {request.MaxTokens} vượt quá giới hạn {maxTokens}");

            var estimatedTokens = EstimateTokens(request.Prompt);
            if (estimatedTokens > maxTokens / 2)
                throw new ArgumentException($"Prompt quá dài: {estimatedTokens} tokens (giới hạn: {maxTokens / 2})");
        }

        private void ValidateEmbeddingRequest(EmbeddingRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                throw new ArgumentException("Text là bắt buộc cho embedding");

            if (request.Text.Length > 8000)
                throw new ArgumentException("Text vượt quá độ dài tối đa 8000 ký tự");
        }

        private PromptExecutionSettings CreateExecutionSettings(AIRequest request, AIModelConfig aiConfig)
        {
            return new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = request.MaxTokens ?? aiConfig?.MaxTokens ?? 2048,
                    ["temperature"] = request.Temperature ?? aiConfig?.Temperature ?? 0.7,
                    ["top_p"] = request.TopP ?? aiConfig?.TopP ?? 0.9,
                    ["system_prompt"] = VIETNAMESE_SYSTEM_PROMPT
                }
            };
        }

        private string PrepareVietnamesePrompt(string userPrompt)
        {
            // Create a conversation format with system message for Vietnamese response
            return $"{VIETNAMESE_SYSTEM_PROMPT}\n\nNgười dùng: {userPrompt}\n\nTrợ lý AI:";
        }

        private string PrepareTextForEmbedding(EmbeddingRequest request)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(request.TypeName))
                parts.Add($"Loại: {request.TypeName}");

            if (!string.IsNullOrEmpty(request.Title))
                parts.Add($"Tiêu đề: {request.Title}");

            if (!string.IsNullOrEmpty(request.Summary))
                parts.Add($"Tóm tắt: {request.Summary}");

            if (request.DepartmentId.HasValue)
                parts.Add($"Phòng ban: {request.DepartmentId}");

            parts.Add("Nội dung:");
            parts.Add(request.Text);

            return string.Join("\n", parts);
        }

        private int? ExtractTokenCount(IReadOnlyDictionary<string, object> metadata)
        {
            if (metadata == null) return null;

            if (metadata.TryGetValue("tokens_used", out var tokensUsed) && tokensUsed != null)
            {
                if (int.TryParse(tokensUsed.ToString(), out var tokens))
                    return tokens;
            }

            return null;
        }

        private int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            // Improved estimation for Vietnamese and mixed content
            var charCount = text.Length;
            var wordCount = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

            return Math.Max(1, (int)Math.Ceiling((charCount / 3.5) + (wordCount / 0.75)));
        }

        private async Task LogMetricsAsync(
            string requestId,
            string sourceService,
            ModelType modelType,
            int tokensUsed,
            long responseTimeMs,
            RequestStatus status,
            string errorMessage)
        {
            try
            {
                await _metricsService.LogUsageAsync(new UsageMetric
                {
                    RequestId = requestId,
                    SourceService = sourceService ?? "Unknown",
                    ModelType = modelType,
                    TokensUsed = tokensUsed,
                    ResponseTimeMs = (int)responseTimeMs,
                    Status = status,
                    ErrorMessage = errorMessage ?? "",
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log metrics for request {RequestId}", requestId);
            }
        }

        private async Task<AIRequestLog> LogRequestStartAsync(string requestId, AIRequest request, ModelType modelType)
        {
            try
            {
                var requestLog = new AIRequestLog
                {
                    RequestId = requestId,
                    SourceService = "AIService",
                    ModelType = modelType,
                    RequestContent = JsonSerializer.Serialize(new
                    {
                        prompt = request.Prompt.Length > 1000 ? request.Prompt.Substring(0, 1000) + "..." : request.Prompt,
                        promptLength = request.Prompt.Length,
                        userId = request.UserId,
                        maxTokens = request.MaxTokens,
                        temperature = request.Temperature,
                        topP = request.TopP,
                        stream = request.Stream
                    }),
                    ResponseContent = "",
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
                _logger.LogError(ex, "Failed to log request start for {RequestId}", requestId);
                return null;
            }
        }

        private async Task LogRequestCompleteAsync(AIRequestLog requestLog, string response, RequestStatus status, int tokensUsed = 0, int responseTimeMs = 0)
        {
            try
            {
                requestLog.ResponseContent = response != null
                    ? JsonSerializer.Serialize(new
                    {
                        answer = response.Length > 1000 ? response.Substring(0, 1000) + "..." : response,
                        responseLength = response.Length
                    })
                    : null;
                requestLog.Status = status;
                requestLog.CompletedAt = DateTime.UtcNow;
                requestLog.TokensUsed = tokensUsed;
                requestLog.ResponseTimeMs = responseTimeMs;

                var repo = _unitOfWork.GetRepository<AIRequestLog>();
                repo.UpdateAsync(requestLog);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log request completion for {RequestId}", requestLog.RequestId);
            }
        }

        #endregion

        public void Dispose()
        {
            _cacheSemaphore?.Dispose();
        }
    }
}
