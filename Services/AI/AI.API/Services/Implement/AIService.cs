
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
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace AI.API.Services.Implement
{
    public class AIService : IAIService
    {
        private readonly ITextGenerationService _defaultTextService;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
        private readonly IAIConfigurationService _configService;
        private readonly IKernelProviderService _kernelProviderService;
        private readonly IMetricsService _metricsService;
        private readonly IUnitOfWork<DocAIDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<AIService> _logger;

        private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);

        private const string VIETNAMESE_SYSTEM_PROMPT = @"Bạn là một trợ lý AI thông minh và hữu ích cho hệ thống tìm kiếm tài liệu nội bộ. 
Hãy luôn trả lời bằng tiếng Việt một cách tự nhiên, lịch sự và chính xác. 
Khi được cung cấp thông tin từ tài liệu, hãy dựa vào đó để trả lời và luôn đề cập đến nguồn tài liệu.
Nếu không có thông tin liên quan trong tài liệu được cung cấp, hãy nói rõ và đưa ra gợi ý tìm kiếm.
Cung cấp thông tin chi tiết, hữu ích và dễ hiểu.";

        public AIService(
            ITextGenerationService defaultTextService,
            IEmbeddingGenerator<string, Embedding<float>> embeddingService,
            IAIConfigurationService configService,
            IKernelProviderService kernelProviderService,
            IMetricsService metricsService,
            IUnitOfWork<DocAIDbContext> unitOfWork,
            IMapper mapper,
            ILogger<AIService> logger)
        {
            _defaultTextService = defaultTextService ?? throw new ArgumentNullException(nameof(defaultTextService));
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _kernelProviderService = kernelProviderService ?? throw new ArgumentNullException(nameof(kernelProviderService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AIResponse> GenerateAnswerAsync(AIRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = Stopwatch.StartNew();
            AIRequestLog? requestLog = null;


            try
            {
                _logger.LogInformation("[{RequestId}] Starting text generation for user: {UserId}", requestId, request.UserId);

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
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    ModelUsed = aiConfig?.ModelId ?? "default",
                    DocumentsUsed = request.Context?.Count ?? 0,
                    ConversationHistoryLength = request.ConversationHistory?.Count ?? 0,
                    DetectedIntent = request.Intent ?? "general",
                    IntentConfidence = !string.IsNullOrEmpty(request.Intent) ? 0.85 : 0.0,
                    ContextTokens = EstimateTokens(CreateContextPrompt(request)) - EstimateTokens(request.Prompt)
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
            AIRequestLog? requestLog = null;
            var chunks = new List<StreamChunk>();
            bool hasError = false;
            string? errorMessage = null;

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
                            RequestId = requestId,
                            HasContext = request.Context?.Any() == true || request.ConversationHistory?.Any() == true,
                            DocumentsCount = request.Context?.Count ?? 0
                        });
                    }
                }

                // Add completion chunk
                chunks.Add(new StreamChunk
                {
                    Content = "",
                    IsComplete = true,
                    TokenCount = totalTokens,
                    RequestId = requestId,
                    HasContext = request.Context?.Any() == true || request.ConversationHistory?.Any() == true,
                    DocumentsCount = request.Context?.Count ?? 0
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

            foreach (var chunk in chunks)
            {
                yield return chunk;
            }
        }
        public async Task<AIResponse> GenerateWithContextAsync(AIContextRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("[{RequestId}] Context-aware generation for user: {UserId}, Documents: {DocCount}, History: {HistoryCount}",
                    requestId, request.UserId, request.DocumentContext?.Count ?? 0, request.ConversationHistory?.Count ?? 0);

                // Get text generation service
                var textService = await GetTextGenerationServiceAsync();
                var aiConfig = await _configService.GetActiveAIModelAsync();

                // Create enhanced prompt with document context
                var enhancedPrompt = CreateContextualPrompt(request);

                // Create execution settings with context adjustments
                var settings = CreateContextualExecutionSettings(request, aiConfig);

                // Generate response
                var result = await textService.GetTextContentsAsync(enhancedPrompt, settings, cancellationToken: cancellationToken);
                var content = result.FirstOrDefault()?.Text ?? "Xin lỗi, tôi không thể tạo phản hồi lúc này.";
                var tokensUsed = ExtractTokenCount(result.FirstOrDefault()?.Metadata) ?? EstimateTokens(content);

                stopwatch.Stop();

                // Log metrics with context info
                await LogMetricsAsync(requestId, request.UserId, ModelType.Chat, tokensUsed,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Completed, null);

                return new AIResponse
                {
                    Success = true,
                    RequestId = requestId,
                    Content = content,
                    TokensUsed = tokensUsed,
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    ModelUsed = aiConfig?.ModelId ?? "default",
                    DocumentsUsed = request.DocumentContext?.Count ?? 0,
                    ConversationHistoryLength = request.ConversationHistory?.Count ?? 0,
                    DetectedIntent = request.Intent ?? "general",
                    IntentConfidence = !string.IsNullOrEmpty(request.Intent) ? 0.85 : 0.0,
                    ContextTokens = EstimateTokens(enhancedPrompt) - EstimateTokens(request.Prompt)
            };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Contextual generation failed", requestId);
                stopwatch.Stop();

                await LogMetricsAsync(requestId, request.UserId, ModelType.Chat, 0,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Failed, ex.Message);

                return new AIResponse
                {
                    Success = false,
                    RequestId = requestId,
                    Message = $"Contextual generation failed: {ex.Message}",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }
        public async IAsyncEnumerable<StreamChunk> StreamWithContextAsync(
          AIContextRequest request,
          [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = Stopwatch.StartNew();
            var totalTokens = 0;
            var fullResponse = new StringBuilder();
            var chunks = new List<StreamChunk>();


            try
            {
                _logger.LogInformation("[{RequestId}] Starting contextual streaming for user: {UserId}", requestId, request.UserId);

                // Get text generation service
                var textService = await GetTextGenerationServiceAsync();
                var aiConfig = await _configService.GetActiveAIModelAsync();

                // Create enhanced prompt with context
                var enhancedPrompt = CreateContextualPrompt(request);
                var settings = CreateContextualExecutionSettings(request, aiConfig);

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
                            RequestId = requestId,
                            HasContext = true,
                            DocumentsCount = request.DocumentContext?.Count ?? 0
                        });
                    }
                }

                // Add completion chunk
                chunks.Add(new StreamChunk
                {
                    Content = "",
                    IsComplete = true,
                    TokenCount = totalTokens,
                    RequestId = requestId,
                    HasContext = true,
                    DocumentsCount = request.DocumentContext?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Contextual streaming failed", requestId);
                chunks.Add(new StreamChunk
                {
                    Content = $"Lỗi contextual streaming: {ex.Message}",
                    IsComplete = true,
                    RequestId = requestId,
                    Error = ex.Message
                });
            }
            finally
            {
                stopwatch.Stop();
                await LogMetricsAsync(requestId, request.UserId, ModelType.Chat, totalTokens,
                    stopwatch.ElapsedMilliseconds, RequestStatus.Completed, null);
            }

            foreach (var chunk in chunks)
            {
                yield return chunk;
            }
        }
        // ========== MODEL MANAGEMENT ==========

        public async Task<AIResponse> GenerateWithModelAsync(string modelId, AIRequest request, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("[{RequestId}] Generation with specific model {ModelId} for user: {UserId}",
                    requestId, modelId, request.UserId);

                // Get specific model configuration
                var modelRepo = _unitOfWork.GetRepository<AIModelConfiguration>();  
                var modelConfig = await modelRepo.SingleOrDefaultAsync(
                    predicate: m => m.ModelId == modelId && m.IsEnabled);

                if (modelConfig == null)
                {
                    _logger.LogWarning("[{RequestId}] Model {ModelId} not found, falling back to default service", requestId, modelId);
                    return await GenerateAnswerAsync(request, cancellationToken); // Fallback to default
                }

                // Kiểm tra API Key
                if (string.IsNullOrEmpty(modelConfig.ApiKey))
                {
                    _logger.LogWarning("[{RequestId}] Model {ModelId} has no API Key, falling back to default service", requestId, modelId);
                    return await GenerateAnswerAsync(request, cancellationToken); // Fallback to default
                }

                try
                {
                    // Create text service for specific model
                    var textService = await _kernelProviderService.CreateTextGenerationServiceAsync(modelConfig);
                    var settings = CreateModelSpecificSettings(request, modelConfig);
                    var enhancedPrompt = PrepareVietnamesePrompt(request.Prompt);

                    // Generate response
                    var result = await textService.GetTextContentsAsync(enhancedPrompt, settings, cancellationToken: cancellationToken);
                    var content = result.FirstOrDefault()?.Text ?? "Xin lỗi, tôi không thể tạo phản hồi lúc này.";
                    var tokensUsed = ExtractTokenCount(result.FirstOrDefault()?.Metadata) ?? EstimateTokens(content);

                    stopwatch.Stop();

                    // Update model usage
                    modelConfig.LastUsedAt = DateTime.UtcNow;
                    modelRepo.UpdateAsync(modelConfig);
                    await _unitOfWork.CommitAsync();

                    await LogMetricsAsync(requestId, request.UserId, ModelType.Chat, tokensUsed,
                        stopwatch.ElapsedMilliseconds, RequestStatus.Completed, null);

                    return new AIResponse
                    {
                        Success = true,
                        RequestId = requestId,
                        Content = content,
                        TokensUsed = tokensUsed,
                        ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                        ModelUsed = modelId
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{RequestId}] Failed to use model {ModelId}, falling back to default service", requestId, modelId);

                    // Fallback to default service khi có lỗi
                    var fallbackResponse = await GenerateAnswerAsync(request, cancellationToken);
                    fallbackResponse.RequestId = requestId; // Keep original request ID
                    fallbackResponse.ModelUsed = $"default (fallback from {modelId})";

                    return fallbackResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Model-specific generation failed completely", requestId);
                stopwatch.Stop();

                return new AIResponse
                {
                    Success = false,
                    RequestId = requestId,
                    Message = $"Generation failed with model {modelId}: {ex.Message}",
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }

        public async Task<List<AIModel>> GetAvailableModelsAsync()
        {
            try
            {
                var modelRepository = _unitOfWork.GetRepository<AIModelConfiguration>();
                var configurations = await modelRepository.GetListAsync(
                    predicate: m => m.IsEnabled,
                    orderBy: q => q.OrderBy(m => m.ProviderType).ThenBy(m => m.Name)
                );

                var models = new List<AIModel>();

                foreach (var config in configurations)
                {
                    // Use AutoMapper for basic mapping
                    var model = _mapper.Map<AIModel>(config);

                    // Set additional properties based on provider type and config
                    await EnhanceModelWithCapabilitiesAndPerformance(model, config);

                    models.Add(model);
                }

                return models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models");
                return new List<AIModel>();
            }
        }

        public async Task<ModelCapabilities> GetModelCapabilitiesAsync(string modelId)
        {
            try
            {
                var modelRepository = _unitOfWork.GetRepository<AIModelConfiguration>();
                var config = await modelRepository.SingleOrDefaultAsync(
                    predicate: m => m.ModelId == modelId
                );

                if (config == null)
                {
                    return new ModelCapabilities
                    {
                        SupportsTextGeneration = false,
                        SupportsStreaming = false,
                        SupportsEmbedding = false,
                        MaxTokens = 0,
                        SupportedLanguages = new List<string>()
                    };
                }

                // Use AutoMapper to map to ModelCapabilities
                return _mapper.Map<ModelCapabilities>(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model capabilities for {ModelId}", modelId);
                return new ModelCapabilities();
            }
        }
        #region Utility Functions

        public async Task<TokenCountResult> CountTokensAsync(string text, string? model = null)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    return new TokenCountResult
                    {
                        Success = false,
                        Message = "Text cannot be empty"
                    };
                }

                var tokenCount = EstimateTokens(text);

                return new TokenCountResult
                {
                    Success = true,
                    DetectedIntent = "token_count",
                    Confidence = 1.0,
                    Message = $"Token count: {tokenCount} for model: {model ?? "default"}"
                };
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

        public async Task<IntentResult> DetectIntentAsync(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    return new IntentResult
                    {
                        Success = false,
                        Message = "Text cannot be empty"
                    };
                }

                // Enhanced intent detection for document search system
                var intents = new Dictionary<string, (List<string> keywords, string description)>
                {
                    ["document_search"] = (new() { "tìm", "search", "tìm kiếm", "tra cứu", "tài liệu", "file", "document" }, "Tìm kiếm tài liệu"),
                    ["question"] = (new() { "?", "gì", "sao", "như thế nào", "tại sao", "có phải", "là gì" }, "Đặt câu hỏi"),
                    ["explanation"] = (new() { "giải thích", "explain", "hướng dẫn", "cách", "làm sao" }, "Yêu cầu giải thích"),
                    ["greeting"] = (new() { "xin chào", "hello", "hi", "chào", "good morning" }, "Chào hỏi"),
                    ["help"] = (new() { "giúp", "help", "hướng dẫn", "trợ giúp", "support" }, "Yêu cầu trợ giúp"),
                    ["summary"] = (new() { "tóm tắt", "summary", "summarize", "overview", "tổng quan" }, "Yêu cầu tóm tắt"),
                    ["comparison"] = (new() { "so sánh", "compare", "khác nhau", "giống", "difference" }, "So sánh thông tin")
                };

                var textLower = text.ToLower();
                var scores = new Dictionary<string, double>();

                foreach (var intent in intents)
                {
                    var matchCount = intent.Value.keywords.Count(keyword => textLower.Contains(keyword));
                    var score = matchCount > 0 ? (double)matchCount / intent.Value.keywords.Count * 1.5 : 0;
                    scores[intent.Key] = Math.Min(1.0, score);
                }

                var topIntent = scores.OrderByDescending(x => x.Value).First();
                var alternatives = scores.Where(x => x.Key != topIntent.Key && x.Value > 0)
                    .Select(x => new IntentPrediction
                    {
                        Intent = x.Key,
                        Confidence = x.Value,
                        Description = intents.ContainsKey(x.Key) ? intents[x.Key].description : x.Key
                    })
                    .OrderByDescending(x => x.Confidence)
                    .Take(3)
                    .ToList();

                return new IntentResult
                {
                    Success = true,
                    DetectedIntent = topIntent.Value > 0 ? topIntent.Key : "general",
                    Confidence = topIntent.Value,
                    AlternativeIntents = alternatives
                };
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

        public async Task<string> SuggestTitleAsync(string content)
        {
            try
            {
                if (string.IsNullOrEmpty(content))
                    return "Cuộc trò chuyện mới";

                var prompt = $@"Dựa vào nội dung cuộc trò chuyện sau, hãy tạo một tiêu đề ngắn gọn (tối đa 8 từ) bằng tiếng Việt:

{content.Substring(0, Math.Min(content.Length, 800))}

Chỉ trả về tiêu đề:";

                var aiRequest = new AIRequest
                {
                    Prompt = prompt,
                    UserId = "system",
                    MaxTokens = 30,
                    Temperature = 0.3
                };

                var response = await GenerateAnswerAsync(aiRequest);

                if (response.Success && !string.IsNullOrEmpty(response.Content))
                {
                    var title = response.Content.Trim().Replace("\"", "").Trim();
                    return string.IsNullOrEmpty(title) ? "Cuộc trò chuyện mới" : title;
                }

                return CreateFallbackTitle(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suggesting title");
                return "Cuộc trò chuyện mới";
            }
        }

        #endregion

        #region Embedding Generation
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
        #endregion
        #region Model Validation

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
                            var textService = await GetTextGenerationServiceAsync();
                            var testPrompt = "Xin chào";
                            var settings = new PromptExecutionSettings
                            {
                                ExtensionData = new Dictionary<string, object> { ["max_tokens"] = 5 }
                            };
                            var result = await textService.GetTextContentsAsync(testPrompt, settings, cancellationToken: cancellationToken);
                            return result?.Any() == true;

                        case ModelType.Embedding:
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

        #endregion
        #region  Private Methods

        private async Task<ITextGenerationService> GetTextGenerationServiceAsync()
        {
            try
            {
                var modelRepository = _unitOfWork.GetRepository<AIModelConfiguration>();
                var modelConfigs = await modelRepository.GetListAsync(
                    predicate: m => m.IsEnabled == true,
                    orderBy: null,
                    include: null);

                var activeModel = modelConfigs.FirstOrDefault(m => m.IsActive && m.IsTestedSuccessfully);

                if (activeModel != null && !string.IsNullOrEmpty(activeModel.ApiKey))
                {
                    _logger.LogDebug("Using active model: {ModelName} ({ProviderType})", activeModel.Name, activeModel.ProviderType);

                    var textService = await _kernelProviderService.CreateTextGenerationServiceAsync(activeModel);

                    activeModel.LastUsedAt = DateTime.UtcNow;
                    modelRepository.UpdateAsync(activeModel);
                    await _unitOfWork.CommitAsync();

                    return textService;
                }

                _logger.LogDebug("No active model found, using default service");
                return _defaultTextService;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get text generation service, using default: {Error}", ex.Message);
                return _defaultTextService;
            }
        }
        private string CreateContextualPrompt(AIContextRequest request)
        {
            var promptBuilder = new StringBuilder();

            promptBuilder.AppendLine(VIETNAMESE_SYSTEM_PROMPT);
            promptBuilder.AppendLine();

            if (request.ConversationHistory?.Any() == true)
            {
                promptBuilder.AppendLine("Lịch sử cuộc trò chuyện:");
                foreach (var message in request.ConversationHistory.TakeLast(5))
                {
                    promptBuilder.AppendLine(message);
                }
                promptBuilder.AppendLine();
            }

            if (request.DocumentContext?.Any() == true)
            {
                promptBuilder.AppendLine("Thông tin từ tài liệu liên quan:");
                promptBuilder.AppendLine();

                for (int i = 0; i < request.DocumentContext.Count; i++)
                {
                    var doc = request.DocumentContext[i];
                    promptBuilder.AppendLine($"[Tài liệu {i + 1}] {doc.Title}");
                    if (!string.IsNullOrEmpty(doc.Summary))
                    {
                        promptBuilder.AppendLine($"Tóm tắt: {doc.Summary}");
                    }
                    promptBuilder.AppendLine($"Nội dung: {doc.Content}");
                    promptBuilder.AppendLine($"Độ liên quan: {doc.RelevanceScore:F2}");
                    promptBuilder.AppendLine();
                }
                promptBuilder.AppendLine("---");
                promptBuilder.AppendLine();
            }

            promptBuilder.AppendLine($"Câu hỏi của người dùng: {request.Prompt}");
            promptBuilder.AppendLine();

            if (request.DocumentContext?.Any() == true)
            {
                promptBuilder.AppendLine("Hãy trả lời dựa trên thông tin từ các tài liệu trên. Nếu không tìm thấy thông tin liên quan, hãy nói rõ và đưa ra gợi ý tìm kiếm khác.");
            }

            return promptBuilder.ToString();
        }
        private string CreateContextPrompt(AIRequest request)
        {
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine(VIETNAMESE_SYSTEM_PROMPT);

            if (request.ConversationHistory?.Any() == true)
            {
                promptBuilder.AppendLine("Lịch sử cuộc trò chuyện:");
                foreach (var message in request.ConversationHistory.TakeLast(5))
                {
                    promptBuilder.AppendLine(message);
                }
                promptBuilder.AppendLine();
            }

            if (request.Context?.Any() == true)
            {
                promptBuilder.AppendLine("Thông tin từ tài liệu liên quan:");
                for (int i = 0; i < request.Context.Count; i++)
                {
                    var doc = request.Context[i];
                    promptBuilder.AppendLine($"[Tài liệu {i + 1}] {doc.Title}");
                    promptBuilder.AppendLine($"Nội dung: {doc.Content}");
                    promptBuilder.AppendLine();
                }
                promptBuilder.AppendLine("---");
            }

            promptBuilder.AppendLine($"Câu hỏi: {request.Prompt}");
            return promptBuilder.ToString();
        }

        private PromptExecutionSettings CreateContextualExecutionSettings(AIContextRequest request, AIModelConfig? aiConfig)
        {
            var baseMaxTokens = request.MaxTokens ?? aiConfig?.MaxTokens ?? 2048;
            var contextTokens = EstimateTokens(CreateContextualPrompt(request)) - EstimateTokens(request.Prompt);
            var adjustedMaxTokens = Math.Max(512, baseMaxTokens - contextTokens);

            return new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = adjustedMaxTokens,
                    ["temperature"] = request.Temperature ?? aiConfig?.Temperature ?? 0.7,
                    ["top_p"] = request.TopP ?? aiConfig?.TopP ?? 0.9,
                    ["context_aware"] = true,
                    ["has_documents"] = request.DocumentContext?.Any() == true,
                    ["has_history"] = request.ConversationHistory?.Any() == true
                }
            };
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

        private PromptExecutionSettings CreateModelSpecificSettings(AIRequest request, AIModelConfiguration modelConfig)
        {
            return new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = Math.Min(request.MaxTokens ?? 2048, GetModelMaxTokens(modelConfig)),
                    ["temperature"] = request.Temperature ?? 0.7,
                    ["top_p"] = request.TopP ?? 0.9,
                    ["system_prompt"] = VIETNAMESE_SYSTEM_PROMPT,
                    ["model_id"] = modelConfig.ModelId,
                    ["provider"] = modelConfig.ProviderType.ToString()
                }
            };
        }

        private string PrepareVietnamesePrompt(string userPrompt)
        {
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

        private string CreateFallbackTitle(string content)
        {
            try
            {
                var words = content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2 && !IsStopWord(w))
                    .Take(5)
                    .ToArray();

                if (words.Length > 0)
                {
                    return string.Join(" ", words);
                }

                return "Cuộc trò chuyện mới";
            }
            catch
            {
                return "Cuộc trò chuyện mới";
            }
        }

        private bool IsStopWord(string word)
        {
            var stopWords = new[] { "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "là", "của", "và", "hoặc", "nhưng", "trong", "trên", "tại", "cho", "với", "bởi" };
            return stopWords.Contains(word.ToLower());
        }

        private int GetModelMaxTokens(AIModelConfiguration modelConfig)
        {
            return modelConfig.ProviderType switch
            {
                AIProviderType.OpenAI when modelConfig.ModelId.Contains("gpt-4") => 8192,
                AIProviderType.OpenAI => 4096,
                AIProviderType.GoogleGemini => 32768,
                AIProviderType.MistralAI => 8192,
                _ => 4096
            };
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

            var charCount = text.Length;
            var wordCount = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

            return Math.Max(1, (int)Math.Ceiling((charCount / 3.5) + (wordCount / 0.75)));
        }

        private async Task EnhanceModelWithCapabilitiesAndPerformance(AIModel model, AIModelConfiguration config)
        {
            // Set capabilities based on provider type
            model.SupportsTextGeneration = true;
            model.SupportsStreaming = true;
            model.SupportsEmbedding = false;
            model.SupportsSystemPrompt = true;
            model.SupportsDocumentAnalysis = true;
            model.SupportedLanguages = "vi,en,zh,ja,ko,fr,de,es";

            model.MaxTokens = config.ProviderType switch
            {
                AIProviderType.OpenAI => config.ModelId.Contains("gpt-4") ? 8192 : 4096,
                AIProviderType.HuggingFace => 4096,
                AIProviderType.MistralAI => 8192,
                AIProviderType.GoogleGemini => 32768,
                AIProviderType.AzureAIInference => 4096,
                _ => 2048
            };

            model.SupportsFunctionCalling = config.ProviderType == AIProviderType.OpenAI ||
                                           config.ProviderType == AIProviderType.MistralAI;

            // Set performance metrics
            try
            {
                var usageRepo = _unitOfWork.GetRepository<UsageMetric>();
                var last30Days = DateTime.UtcNow.AddDays(-30);

                var modelUsage = await usageRepo.GetListAsync(
                    predicate: u => u.CreatedAt >= last30Days,
                    orderBy: null);

                var relevantUsage = modelUsage.Where(u => u.RequestId.Contains(config.ModelId)).ToList();

                if (relevantUsage.Any())
                {
                    model.AverageResponseTime = relevantUsage.Average(u => u.ResponseTimeMs);
                    model.TotalRequests = relevantUsage.Count;
                    model.SuccessRate = relevantUsage.Count(u => u.Status == RequestStatus.Completed) * 100.0 / relevantUsage.Count;
                }
                else
                {
                    model.AverageResponseTime = config.AverageResponseTime ?? 0;
                    model.TotalRequests = 0;
                    model.SuccessRate = config.IsTestedSuccessfully ? 100.0 : 0.0;
                }

                model.LastUsed = config.LastUsedAt;
                model.LastTested = config.LastTestedAt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting performance for model {ModelId}", config.ModelId);
                model.AverageResponseTime = 0;
                model.TotalRequests = 0;
                model.SuccessRate = 0;
            }
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
                    UserId = request.UserId,
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
