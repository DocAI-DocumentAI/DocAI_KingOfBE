using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AutoMapper;
using ChatBox.API.Payload.Request.AIClientService;
using ChatBox.API.Payload.Response;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using ChatBox.API.Services.Interfaces;
using Polly.CircuitBreaker;
using Polly;
using Microsoft.Extensions.Options;

namespace ChatBox.API.Services.Implement
{
    public class AiServiceClient : IAiServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiServiceClient> _logger;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IMapper _mapper;

        public AiServiceClient(
             HttpClient httpClient,
             ILogger<AiServiceClient> logger,
             IConfiguration configuration,
             IMapper mapper)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<AiGenerationResult> GenerateResponseAsync(AdvancedAiGenerationRequest request)
        {
            try
            {
                _logger.LogInformation("Generating AI response for query length: {QueryLength}", request.Query?.Length ?? 0);

                var aiRequest = new
                {
                    prompt = request.Query,
                    userId = "chatbox-service", // Service identifier for AI service
                    context = ConvertContextToAiFormat(request.Context),
                    conversationHistory = request.ConversationHistory ?? new List<string>(),
                    maxTokens = request.MaxTokens ,
                    temperature = request.Temperature ,
                    modelId = request.Model
                };
                var response = await PostAsync<AIServiceResponse>("/api/ai/generate", aiRequest);


                if (response != null && response.Success)
                {
                    _logger.LogInformation("AI response received successfully, TokensUsed: {TokensUsed}",
                        response.TokensUsed);

                    return new AiGenerationResult
                    {
                        Success = true,
                        Response = response.Content,
                        TokensUsed = response.TokensUsed,
                        Model = response.ModelUsed ?? "unknown",
                        ConfidenceScore = response.IntentConfidence,
                        ProcessingTime = TimeSpan.FromMilliseconds(response.ResponseTimeMs),
                        Metadata = new Dictionary<string, object>
                        {
                            { "RequestId", response.RequestId },
                            { "DocumentsUsed", response.DocumentsUsed },
                            { "ContextTokens", response.ContextTokens },
                            { "DetectedIntent", response.DetectedIntent ?? "general" }
                        }
                    };
                }

                _logger.LogWarning("AI generation failed or returned empty response");
                return CreateFallbackGenerationResult(request.Query);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP error calling AI service for generation");
                return CreateFallbackGenerationResult(request.Query, $"AI service unavailable: {httpEx.Message}");
            }
            catch (TaskCanceledException tcEx)
            {
                _logger.LogError(tcEx, "Timeout calling AI service for generation");
                return CreateFallbackGenerationResult(request.Query, "AI service timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AI service for generation");
                return CreateFallbackGenerationResult(request.Query, ex.Message);
            }
        }

        public async Task<IAsyncEnumerable<StreamingChunk>> StreamResponseAsync(StreamingRequest request)
        {
            try
            {
                _logger.LogInformation("Starting streaming request to AI service, StreamId: {StreamId}",
                    request.StreamId);

                return StreamResponseInternalAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting streaming response");
                return CreateErrorStream(request.StreamId, ex.Message);
            }
        }

        public async Task<int> CountTokensAsync(string text, string model = "default")
        {
            try
            {
                var request = new
                {
                    text = text,
                    model = model
                };

                var response = await PostAsync<TokenCountServiceResponse>("/api/ai/tokens/count", request);

                if (response != null && response.Success)
                {
                    return response.TokenCount;
                }

                _logger.LogWarning("Token count API failed, using estimation");
                return EstimateTokenCount(text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error counting tokens, using estimation");
                return EstimateTokenCount(text);
            }
        }


        public async Task<string> TruncateToTokenLimitAsync(string text, int maxTokens)
        {
            try
            {
                // AI service doesn't have truncate endpoint, so we implement client-side
                var currentTokens = await CountTokensAsync(text);
                if (currentTokens <= maxTokens)
                {
                    return text;
                }

                return TruncateByEstimation(text, maxTokens);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error truncating text, using estimation");
                return TruncateByEstimation(text, maxTokens);
            }
        }

        public async Task<IntentDetectionResult> DetectIntentAsync(IntentDetectionRequest request)
        {
            try
            {
                _logger.LogDebug("Detecting intent for text: {TextPreview}",
                    request.Text?.Length > 50 ? request.Text.Substring(0, 50) + "..." : request.Text);

                var aiRequest = new
                {
                    text = request.Text
                };

                var response = await PostAsync<IntentServiceResponse>("/api/ai/intent/detect", aiRequest);

                if (response != null && response.Success)
                {
                    return new IntentDetectionResult
                    {
                        PredictedIntent = response.DetectedIntent ?? "general",
                        Confidence = response.Confidence,
                        AllIntentScores = response.AlternativeIntents?.Select(a => new IntentScore
                        {
                            Intent = a.Intent,
                            Score = a.Confidence
                        }).ToList() ?? new List<IntentScore>(),
                        ExtractedParameters = new Dictionary<string, object>(),
                        RequiresClarification = response.Confidence < 0.7,
                        ClarificationQuestions = response.Confidence < 0.7
                            ? new List<string> { "Bạn có thể cung cấp thêm thông tin chi tiết về yêu cầu của mình không?" }
                            : new List<string>()
                    };
                }

                return CreateFallbackIntentDetection(request.Text, request.PossibleIntents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting intent");
                return CreateFallbackIntentDetection(request.Text, request.PossibleIntents);
            }
        }

        public async Task<string> SuggestTitleAsync(TitleSuggestionRequest request)
        {
            try
            {
                var aiRequest = new
                {
                    content = request.Content,
                    maxLength = request.MaxLength
                };

                var response = await PostAsync<object>("/api/ai/title/suggest", aiRequest);

                if (response != null)
                {
                    // Parse the response - AI service returns different format
                    var responseStr = response.ToString();
                    if (!string.IsNullOrEmpty(responseStr) && responseStr != "{}")
                    {
                        try
                        {
                            var titleObj = JsonSerializer.Deserialize<Dictionary<string, object>>(responseStr, _jsonOptions);
                            if (titleObj != null && titleObj.ContainsKey("title"))
                            {
                                return titleObj["title"]?.ToString() ?? GenerateFallbackTitle(request.Content);
                            }
                        }
                        catch
                        {
                            // If JSON parsing fails, treat as plain string
                            return responseStr.Length > 50 ? responseStr.Substring(0, 50) : responseStr;
                        }
                    }
                }

                return GenerateFallbackTitle(request.Content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error suggesting title, using fallback");
                return GenerateFallbackTitle(request.Content);
            }
        }

        private async Task<T?> PostAsync<T>(string endpoint, object request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Calling AI service endpoint: {Endpoint} with payload: {Payload}",
                    endpoint, json.Length > 1000 ? json.Substring(0, 1000) + "..." : json);

                var response = await _httpClient.PostAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrEmpty(responseJson))
                    {
                        _logger.LogWarning("AI Service returned empty response for endpoint: {Endpoint}", endpoint);
                        return default(T);
                    }

                    _logger.LogDebug("AI Service response: {Response}",
                        responseJson.Length > 1000 ? responseJson.Substring(0, 1000) + "..." : responseJson);

                    return JsonSerializer.Deserialize<T>(responseJson, _jsonOptions);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("AI Service API call failed. Endpoint: {Endpoint}, Status: {StatusCode}, Content: {Content}",
                    endpoint, response.StatusCode, errorContent);

                return default(T);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AI service endpoint: {Endpoint}", endpoint);
                return default(T);
            }
        }
        private async IAsyncEnumerable<StreamingChunk> StreamResponseInternalAsync(StreamingRequest request,
          [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ai/stream");

            // Map to AI service stream request format
            var aiRequest = new
            {
                prompt = request.Query,
                userId = "chatbox-service",
                context = ConvertContextToAiFormat(request.Context),
                conversationHistory = request.ConversationHistory ?? new List<string>(),
                maxTokens = request.MaxTokens,
                temperature = request.Temperature,
                modelId = request.Model
            };

            var json = JsonSerializer.Serialize(aiRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage? response = null;
            Stream? stream = null;
            StreamReader? reader = null;
            bool hasError = false;
            string? errorMessage = null;
            var chunks = new List<StreamingChunk>();

            // Execute HTTP request and collect all chunks first
            try
            {
                response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("AI service streaming failed: {StatusCode} - {Content}", response.StatusCode, errorContent);

                    hasError = true;
                    errorMessage = $"AI service returned {response.StatusCode}";
                }
                else
                {
                    stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    reader = new StreamReader(stream);

                    var chunkIndex = 0;
                    string? line;

                    while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
                    {
                        if (line.StartsWith("data: "))
                        {
                            var data = line.Substring(6); // Remove "data: " prefix

                            if (string.IsNullOrWhiteSpace(data) || data == "[DONE]")
                                continue;

                            try
                            {
                                var streamChunk = JsonSerializer.Deserialize<AIStreamChunk>(data, _jsonOptions);

                                if (streamChunk != null)
                                {
                                    chunks.Add(new StreamingChunk
                                    {
                                        StreamId = request.StreamId,
                                        Content = streamChunk.Content,
                                        ChunkIndex = chunkIndex++,
                                        IsComplete = streamChunk.IsComplete,
                                        ChunkType = streamChunk.IsComplete ? "completion" : "text",
                                        Metadata = new Dictionary<string, object>
                                        {
                                            { "TokenCount", streamChunk.TokenCount },
                                            { "HasContext", streamChunk.HasContext },
                                            { "DocumentsCount", streamChunk.DocumentsCount }
                                        },
                                        Timestamp = DateTime.UtcNow
                                    });

                                    if (streamChunk.IsComplete)
                                        break;
                                }
                            }
                            catch (JsonException ex)
                            {
                                _logger.LogWarning(ex, "Failed to parse streaming chunk: {Data}", data);
                                // Continue processing other chunks
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Streaming cancelled for StreamId: {StreamId}", request.StreamId);
                chunks.Clear();
                chunks.Add(new StreamingChunk
                {
                    StreamId = request.StreamId,
                    Content = "",
                    ChunkIndex = 0,
                    IsComplete = true,
                    ChunkType = "cancelled",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during streaming");
                hasError = true;
                errorMessage = ex.Message;
            }
            finally
            {
                // Clean up resources
                reader?.Dispose();
                stream?.Dispose();
                response?.Dispose();
            }

            // Now yield return all collected chunks (outside try-catch)
            if (hasError)
            {
                yield return new StreamingChunk
                {
                    StreamId = request.StreamId,
                    Content = $"Lỗi streaming: {errorMessage}",
                    ChunkIndex = 0,
                    IsComplete = true,
                    ChunkType = "error",
                    Timestamp = DateTime.UtcNow
                };
            }
            else
            {
                foreach (var chunk in chunks)
                {
                    yield return chunk;
                }

                // Ensure we have a completion chunk if not already present
                if (chunks.Count == 0 || !chunks.Last().IsComplete)
                {
                    yield return new StreamingChunk
                    {
                        StreamId = request.StreamId,
                        Content = "",
                        ChunkIndex = chunks.Count,
                        IsComplete = true,
                        ChunkType = "completion",
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
        }

        private async IAsyncEnumerable<StreamingChunk> CreateErrorStream(string streamId, string error)
        {
            yield return new StreamingChunk
            {
                StreamId = streamId,
                Content = "Xin lỗi, đã xảy ra lỗi khi xử lý yêu cầu của bạn.",
                ChunkIndex = 0,
                IsComplete = true,
                ChunkType = "error",
                Metadata = new Dictionary<string, object> { { "Error", error } },
                Timestamp = DateTime.UtcNow
            };
        }
        private List<object>? ConvertContextToAiFormat(string? context)
        {
            if (string.IsNullOrEmpty(context))
                return null;

            try
            {
                // Try to parse as JSON array first
                var contextArray = JsonSerializer.Deserialize<List<DocumentContext>>(context, _jsonOptions);
                if (contextArray != null)
                {
                    return contextArray.Select(doc => new
                    {
                        title = doc.Title,
                        content = doc.Content,
                        relevanceScore = doc.RelevanceScore,
                        summary = doc.Summary
                    }).Cast<object>().ToList();
                }
            }
            catch
            {
                // If not JSON, treat as single text context
                return new List<object>
                {
                    new
                    {
                        title = "Context",
                        content = context,
                        relevanceScore = 1.0
                    }
                };
            }

            return null;
        }

        private AiGenerationResult CreateFallbackGenerationResult(string query, string? error = null)
        {
            return new AiGenerationResult
            {
                Success = false,
                Response = "Xin lỗi, tôi không thể tạo phản hồi lúc này. Vui lòng thử lại sau.",
                TokensUsed = 0,
                Model = "fallback",
                ConfidenceScore = 0.0,
                ProcessingTime = TimeSpan.Zero,
                Metadata = new Dictionary<string, object>
                {
                    { "Error", error ?? "AI service unavailable" },
                    { "QueryLength", query?.Length ?? 0 },
                    { "FallbackUsed", true }
                }
            };
        }

        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Estimate for Vietnamese/English mix: ~3.5 characters per token
            return (int)Math.Ceiling(text.Length / 3.5);
        }

        private string TruncateByEstimation(string text, int maxTokens)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var estimatedMaxChars = maxTokens * 3; // Conservative estimate
            if (text.Length <= estimatedMaxChars)
                return text;

            // Find a good breaking point (sentence or word boundary)
            var truncateAt = estimatedMaxChars - 3;
            var breakPoint = text.LastIndexOfAny(new[] { '.', '!', '?', '\n' }, truncateAt);

            if (breakPoint > truncateAt / 2) // If we found a good break point
            {
                return text.Substring(0, breakPoint + 1).Trim();
            }

            // Otherwise, break at word boundary
            breakPoint = text.LastIndexOf(' ', truncateAt);
            if (breakPoint > truncateAt / 2)
            {
                return text.Substring(0, breakPoint).Trim() + "...";
            }

            return text.Substring(0, truncateAt) + "...";
        }

        private IntentDetectionResult CreateFallbackIntentDetection(string? text, List<string>? possibleIntents)
        {
            var intent = possibleIntents?.FirstOrDefault() ?? DetectBasicIntent(text ?? "");

            return new IntentDetectionResult
            {
                PredictedIntent = intent,
                Confidence = 0.5,
                AllIntentScores = possibleIntents?.Select(i => new IntentScore { Intent = i, Score = 0.5 }).ToList() ?? new List<IntentScore>(),
                ExtractedParameters = new Dictionary<string, object>(),
                RequiresClarification = false,
                ClarificationQuestions = new List<string>()
            };
        }

        private string DetectBasicIntent(string content)
        {
            var lowerContent = content.ToLower();

            // Vietnamese patterns
            if (lowerContent.Contains("?") || lowerContent.Contains("gì") || lowerContent.Contains("sao") ||
                lowerContent.Contains("như thế nào") || lowerContent.Contains("tại sao"))
                return "question";
            else if (lowerContent.Contains("help") || lowerContent.Contains("giúp") ||
                     lowerContent.Contains("hướng dẫn") || lowerContent.Contains("trợ giúp"))
                return "help_request";
            else if (lowerContent.Contains("thank") || lowerContent.Contains("cảm ơn") ||
                     lowerContent.Contains("thanks") || lowerContent.Contains("cám ơn"))
                return "gratitude";
            else if (lowerContent.Contains("tìm") || lowerContent.Contains("search") ||
                     lowerContent.Contains("tìm kiếm") || lowerContent.Contains("tài liệu"))
                return "document_search";
            else if (lowerContent.Contains("xin chào") || lowerContent.Contains("hello") ||
                     lowerContent.Contains("hi") || lowerContent.Contains("chào"))
                return "greeting";
            else
                return "general";
        }

        private string GenerateFallbackTitle(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "Cuộc trò chuyện mới";

            var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var title = string.Join(" ", words.Take(5));

            if (title.Length > 50)
                title = title.Substring(0, 47) + "...";

            return title;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class AIServiceResponse
    {
        public bool Success { get; set; }
        public string RequestId { get; set; }
        public string Content { get; set; }
        public int TokensUsed { get; set; }
        public int ResponseTimeMs { get; set; }
        public string? ModelUsed { get; set; }
        public string? Message { get; set; }
        public int DocumentsUsed { get; set; }
        public int ConversationHistoryLength { get; set; }
        public string? DetectedIntent { get; set; }
        public double IntentConfidence { get; set; }
        public int ContextTokens { get; set; }
    }

    public class AIStreamChunk
    {
        public string Content { get; set; }
        public bool IsComplete { get; set; }
        public int? TokenCount { get; set; }
        public string RequestId { get; set; }
        public string? Error { get; set; }
        public bool HasContext { get; set; }
        public int DocumentsCount { get; set; }
    }

    public class TokenCountServiceResponse
    {
        public bool Success { get; set; }
        public int TokenCount { get; set; }
        public string? Message { get; set; }
    }

    public class IntentServiceResponse
    {
        public bool Success { get; set; }
        public string? DetectedIntent { get; set; }
        public double Confidence { get; set; }
        public List<AlternativeIntent>? AlternativeIntents { get; set; }
        public string? Message { get; set; }
    }

    public class AlternativeIntent
    {
        public string Intent { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string? Description { get; set; }
    }
    public class DocumentContext
    {
        public string DocumentId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string? Summary { get; set; }
        public double RelevanceScore { get; set; }
        public string? DocumentType { get; set; }
        public string? DepartmentId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
