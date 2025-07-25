using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AutoMapper;
using ChatBox.API.Payload.Request.AIClientService;
using ChatBox.API.Payload.Response;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;
using ChatBox.API.Payload.Response.HealthMonitoringResponses;
using ChatBox.API.Services.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class AiServiceClient : IAiServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiServiceClient> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _aiServiceBaseUrl;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IMapper _mapper;

        public AiServiceClient(
            HttpClient httpClient,
            ILogger<AiServiceClient> logger,
            IConfiguration configuration,
            IMapper mapper)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _mapper = mapper;
            _aiServiceBaseUrl = configuration["Services:AIService:BaseUrl"] ?? "http://localhost:5002";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            ConfigureHttpClient();
        }

        public async Task<AiGenerationResult> GenerateResponseAsync(AdvancedAiGenerationRequest request)
        {
            try
            {
                _logger.LogInformation("Generating AI response for query length: {QueryLength}", request.Query?.Length ?? 0);

                var response = await PostAsync<AiGenerationResult>("/api/ai/generate", request);

                if (response.Success)
                {
                    _logger.LogInformation("AI response generated successfully, TokensUsed: {TokensUsed}", response.TokensUsed);
                }
                else
                {
                    _logger.LogWarning("AI generation failed: {Response}", response.Response);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response");
                return new AiGenerationResult
                {
                    Success = false,
                    Response = "I apologize, but I'm unable to generate a response at this time. Please try again later.",
                    TokensUsed = 0,
                    Model = "fallback",
                    ConfidenceScore = 0.0,
                    ProcessingTime = TimeSpan.Zero,
                    Metadata = new Dictionary<string, object> { { "Error", ex.Message } }
                };
            }
        }

        public async Task<IAsyncEnumerable<StreamingChunk>> StreamResponseAsync(StreamingRequest request)
        {
            try
            {
                _logger.LogInformation("Starting streaming response for StreamId: {StreamId}", request.StreamId);

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
                var requestObj = new { Text = text, Model = model };
                var response = await PostAsync<TokenCountResponse>("/api/ai/tokens/count", requestObj);

                return response?.TokenCount ?? EstimateTokenCount(text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error counting tokens, using estimation");
                return EstimateTokenCount(text);
            }
        }

        public async Task<TokenBreakdown> EstimateFullTokenUsageAsync(EstimateTokenRequest request)
        {
            try
            {
                _logger.LogDebug("Estimating token usage for input length: {InputLength}", request.Input?.Length ?? 0);

                var response = await PostAsync<TokenBreakdown>("/api/ai/tokens/estimate", request);

                if (response != null)
                {
                    return response;
                }

                // Fallback estimation using AutoMapper
                var fallbackResponse = _mapper.Map<TokenBreakdown>(new object());
                fallbackResponse.InputTokens = EstimateTokenCount(request.Input ?? string.Empty);
                fallbackResponse.OutputTokens = EstimateTokenCount(string.Join(" ", request.ConversationHistory ?? new List<string>()));
                fallbackResponse.TotalTokens = fallbackResponse.InputTokens + fallbackResponse.OutputTokens;
                fallbackResponse.EstimatedCost = 0.0m;

                return fallbackResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating token usage");
                return new TokenBreakdown
                {
                    InputTokens = EstimateTokenCount(request.Input),
                    OutputTokens = 0,
                    TotalTokens = EstimateTokenCount(request.Input),
                    EstimatedCost = 0.0m
                };
            }
        }

        public async Task<string> TruncateToTokenLimitAsync(string text, int maxTokens)
        {
            try
            {
                var request = new { Text = text, MaxTokens = maxTokens };
                var response = await PostAsync<TruncateResponse>("/api/ai/tokens/truncate", request);

                return response?.TruncatedText ?? TruncateByEstimation(text, maxTokens);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error truncating text, using estimation");
                return TruncateByEstimation(text, maxTokens);
            }
        }

        public async Task<MessageAnalysisResult> AnalyzeContentAsync(ContentAnalysisRequest request)
        {
            try
            {
                _logger.LogDebug("Analyzing content of length: {ContentLength}", request.Content?.Length ?? 0);

                var response = await PostAsync<MessageAnalysisResult>("/api/ai/analyze", request);

                if (response != null)
                {
                    return response;
                }

                // Fallback analysis
                return CreateFallbackAnalysis(request.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing content");
                return CreateFallbackAnalysis(request.Content);
            }
        }

        public async Task<string> DetectLanguageAsync(string content)
        {
            try
            {
                var request = new { Content = content };
                var response = await PostAsync<LanguageDetectionResponse>("/api/ai/language/detect", request);

                return response?.Language ?? "en";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error detecting language, defaulting to English");
                return "en";
            }
        }

        public async Task<ConversationSummaryResult> GenerateConversationSummaryAsync(ConversationSummaryRequest request)
        {
            try
            {
                _logger.LogInformation("Generating conversation summary for {MessageCount} messages",
                    request.ConversationHistory?.Count ?? 0);

                var response = await PostAsync<ConversationSummaryResult>("/api/ai/summarize/conversation", request);

                if (response != null)
                {
                    return response;
                }

                // Fallback summary
                return CreateFallbackSummary(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating conversation summary");
                return CreateFallbackSummary(request);
            }
        }

        public async Task<IntentDetectionResult> DetectIntentAsync(IntentDetectionRequest request)
        {
            try
            {
                _logger.LogDebug("Detecting intent for text: {TextPreview}",
                    request.Text?.Length > 50 ? request.Text.Substring(0, 50) + "..." : request.Text);

                var response = await PostAsync<IntentDetectionResult>("/api/ai/intent/detect", request);

                if (response != null)
                {
                    return response;
                }

                // Fallback intent detection
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
                var response = await PostAsync<TitleSuggestionResponse>("/api/ai/title/suggest", request);

                return response?.Title ?? GenerateFallbackTitle(request.Content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error suggesting title, using fallback");
                return GenerateFallbackTitle(request.Content);
            }
        }

        public async Task<string> TranslateTextAsync(TranslationRequest request)
        {
            try
            {
                _logger.LogDebug("Translating text to {TargetLanguage}", request.TargetLanguage);

                var response = await PostAsync<TranslationResponse>("/api/ai/translate", request);

                return response?.TranslatedText ?? request.Text; // Return original if translation fails
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating text");
                return request.Text; // Return original text on error
            }
        }

        public async Task<string> SummarizeTextAsync(string text, int maxLength = 200)
        {
            try
            {
                var request = new { Text = text, MaxLength = maxLength };
                var response = await PostAsync<SummarizationResponse>("/api/ai/summarize", request);

                return response?.Summary ?? CreateFallbackTextSummary(text, maxLength);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error summarizing text");
                return CreateFallbackTextSummary(text, maxLength);
            }
        }

        public async Task<List<AvailableModel>> GetAvailableModelsAsync()
        {
            try
            {
                var response = await GetAsync<List<AvailableModel>>("/api/ai/models");

                return response ?? CreateFallbackModelList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models");
                return CreateFallbackModelList();
            }
        }

        // Private helper methods
        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_aiServiceBaseUrl);
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ChatBox-Service/1.0");

            // Add API key if configured
            var apiKey = _configuration["Services:AIService:ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        }

        private async Task<T> PostAsync<T>(string endpoint, object request)
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseJson, _jsonOptions);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("AI Service API call failed. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, errorContent);

            return default(T);
        }

        private async Task<T> GetAsync<T>(string endpoint)
        {
            var response = await _httpClient.GetAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseJson, _jsonOptions);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("AI Service API call failed. Status: {StatusCode}, Content: {Content}",
                response.StatusCode, errorContent);

            return default(T);
        }

        private async IAsyncEnumerable<StreamingChunk> StreamResponseInternalAsync(StreamingRequest request)
        {
            var chunkIndex = 0;
            var chunks = await SimulateStreamingChunks(request);

            foreach (var chunk in chunks)
            {
                yield return new StreamingChunk
                {
                    StreamId = request.StreamId,
                    Content = chunk,
                    ChunkIndex = chunkIndex++,
                    IsComplete = false,
                    ChunkType = "text",
                    Timestamp = DateTime.UtcNow
                };

                await Task.Delay(100); // Simulate streaming delay
            }

            // Final chunk to indicate completion
            yield return new StreamingChunk
            {
                StreamId = request.StreamId,
                Content = "",
                ChunkIndex = chunkIndex,
                IsComplete = true,
                ChunkType = "completion",
                Timestamp = DateTime.UtcNow
            };
        }

        private async IAsyncEnumerable<StreamingChunk> CreateErrorStream(string streamId, string error)
        {
            yield return new StreamingChunk
            {
                StreamId = streamId,
                Content = "I apologize, but I encountered an error while processing your request.",
                ChunkIndex = 0,
                IsComplete = true,
                ChunkType = "error",
                Metadata = new Dictionary<string, object> { { "Error", error } },
                Timestamp = DateTime.UtcNow
            };
        }

        private async Task<List<string>> SimulateStreamingChunks(StreamingRequest request)
        {
            // In a real implementation, this would call the actual AI service streaming endpoint
            // For now, simulate by breaking down a generated response

            try
            {
                var generationRequest = new AdvancedAiGenerationRequest
                {
                    Query = request.Query,
                    Context = request.Context,
                    ConversationHistory = request.ConversationHistory,
                    UserPreferences = request.UserPreferences,
                    MaxTokens = request.MaxTokens,
                    Temperature = request.Temperature,
                    Model = request.Model
                };

                var result = await GenerateResponseAsync(generationRequest);

                if (result.Success)
                {
                    return SplitIntoChunks(result.Response, 50);
                }
                else
                {
                    return new List<string> { "I apologize, but I couldn't generate a response." };
                }
            }
            catch
            {
                return new List<string> { "An error occurred while generating the response." };
            }
        }

        private List<string> SplitIntoChunks(string text, int chunkSize)
        {
            var chunks = new List<string>();
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                var length = Math.Min(chunkSize, text.Length - i);
                chunks.Add(text.Substring(i, length));
            }
            return chunks;
        }

        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Rough estimation: 1 token ≈ 4 characters
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        private string TruncateByEstimation(string text, int maxTokens)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var estimatedMaxChars = maxTokens * 4;
            if (text.Length <= estimatedMaxChars)
                return text;

            return text.Substring(0, estimatedMaxChars - 3) + "...";
        }

        private MessageAnalysisResult CreateFallbackAnalysis(string content)
        {
            var intent = DetectBasicIntent(content);
            var sentiment = DetectBasicSentiment(content);

            return new MessageAnalysisResult
            {
                Intent = intent,
                IntentConfidence = 0.5,
                Sentiment = sentiment,
                SentimentScore = 0.5,
                DetectedEntities = new List<string>(),
                DetectedTopics = ExtractBasicTopics(content),
                Language = "en",
                LanguageConfidence = 0.8,
                AdditionalMetadata = new Dictionary<string, object>
                {
                    { "FallbackAnalysis", true }
                }
            };
        }

        private string DetectBasicIntent(string content)
        {
            var lowerContent = content.ToLower();

            if (lowerContent.Contains("?"))
                return "question";
            else if (lowerContent.Contains("help") || lowerContent.Contains("assist"))
                return "help_request";
            else if (lowerContent.Contains("thank") || lowerContent.Contains("thanks"))
                return "gratitude";
            else if (lowerContent.Contains("sorry") || lowerContent.Contains("apologize"))
                return "apology";
            else
                return "general";
        }

        private string DetectBasicSentiment(string content)
        {
            var lowerContent = content.ToLower();
            var positiveWords = new[] { "good", "great", "excellent", "amazing", "wonderful", "fantastic" };
            var negativeWords = new[] { "bad", "terrible", "awful", "horrible", "disappointing", "frustrating" };

            var positiveCount = positiveWords.Count(word => lowerContent.Contains(word));
            var negativeCount = negativeWords.Count(word => lowerContent.Contains(word));

            if (positiveCount > negativeCount)
                return "positive";
            else if (negativeCount > positiveCount)
                return "negative";
            else
                return "neutral";
        }

        private List<string> ExtractBasicTopics(string content)
        {
            var topics = new List<string>();
            var lowerContent = content.ToLower();

            var topicKeywords = new Dictionary<string, string[]>
            {
                { "HR", new[] { "hr", "human resources", "employee", "policy", "benefits" } },
                { "IT", new[] { "it", "computer", "software", "technical", "system" } },
                { "Finance", new[] { "finance", "budget", "money", "cost", "payment" } },
                { "Legal", new[] { "legal", "contract", "compliance", "regulation" } }
            };

            foreach (var topic in topicKeywords)
            {
                if (topic.Value.Any(keyword => lowerContent.Contains(keyword)))
                {
                    topics.Add(topic.Key);
                }
            }

            return topics;
        }

        private ConversationSummaryResult CreateFallbackSummary(ConversationSummaryRequest request)
        {
            var messageCount = request.ConversationHistory?.Count ?? 0;
            var totalLength = request.ConversationHistory?.Sum(m => m.Length) ?? 0;

            return new ConversationSummaryResult
            {
                Summary = $"This conversation contained {messageCount} messages discussing various topics.",
                KeyPoints = new List<string> { "General discussion", "Information exchange" },
                ActionItems = new List<string>(),
                Topics = new List<string> { "General" },
                OriginalLength = totalLength,
                SummaryLength = 50,
                CompressionRatio = totalLength > 0 ? 50.0 / totalLength : 0,
                SummaryType = request.SummaryType
            };
        }

        private IntentDetectionResult CreateFallbackIntentDetection(string text, List<string> possibleIntents)
        {
            var intent = possibleIntents?.FirstOrDefault() ?? DetectBasicIntent(text);

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

        private string GenerateFallbackTitle(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "New Conversation";

            var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var title = string.Join(" ", words.Take(5));

            if (title.Length > 50)
                title = title.Substring(0, 47) + "...";

            return title;
        }

        private string CreateFallbackTextSummary(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - 3) + "...";
        }

        private List<AvailableModel> CreateFallbackModelList()
        {
            return new List<AvailableModel>
            {
                new AvailableModel
                {
                    Id = "default",
                    Name = "Default Model",
                    Description = "Default AI model for text generation",
                    Type = "generation",
                    MaxTokens = 4000,
                    SupportsStreaming = true,
                    Status = "available"
                }
            };
        }
    }

    // Response DTOs for internal API calls
    public class TokenCountResponse
    {
        public int TokenCount { get; set; }
    }

    public class TruncateResponse
    {
        public string TruncatedText { get; set; }
    }

    public class LanguageDetectionResponse
    {
        public string Language { get; set; }
        public double Confidence { get; set; }
    }

    public class TitleSuggestionResponse
    {
        public string Title { get; set; }
    }

    public class TranslationResponse
    {
        public string TranslatedText { get; set; }
        public string DetectedSourceLanguage { get; set; }
        public double Confidence { get; set; }
    }

    public class SummarizationResponse
    {
        public string Summary { get; set; }
        public int OriginalLength { get; set; }
        public int SummaryLength { get; set; }
    }
}
