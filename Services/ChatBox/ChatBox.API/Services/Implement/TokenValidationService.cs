using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ChatBox.API.Services.Implement
{
    public class TokenValidationService : ITokenValidationService
    {
        private readonly IAiServiceClient _aiServiceClient;
        private readonly IMemoryCache _memoryCache;
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly ILogger<TokenValidationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;

        // Token estimation constants based on OpenAI's tokenizer
        private const double AVERAGE_CHARS_PER_TOKEN = 4.0;
        private const double WORD_TO_TOKEN_RATIO = 1.3;
        private const int CACHE_DURATION_MINUTES = 30;

        // Model-specific token limits
        private static readonly Dictionary<string, int> ModelTokenLimits = new()
        {
            { "gpt-3.5-turbo", 4096 },
            { "gpt-3.5-turbo-16k", 16384 },
            { "gpt-4", 8192 },
            { "gpt-4-32k", 32768 },
            { "gpt-4-turbo", 128000 },
            { "claude-3-sonnet", 200000 },
            { "claude-3-opus", 200000 },
            { "default", 4096 }
        };

        public TokenValidationService(
            IAiServiceClient aiServiceClient,
            IMemoryCache memoryCache,
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            ILogger<TokenValidationService> logger,
            IConfiguration configuration)
        {
            _aiServiceClient = aiServiceClient;
            _memoryCache = memoryCache;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<TokenBreakdown> EstimateTokenUsageAsync(string input, string systemPrompt, List<string> history)
        {
            try
            {
                _logger.LogDebug("Estimating token usage for input length: {InputLength}, history items: {HistoryCount}",
                    input?.Length ?? 0, history?.Count ?? 0);

                var breakdown = new TokenBreakdown
                {
                    EstimationTimestamp = DateTime.UtcNow,
                    EstimationMethod = "hybrid_estimation",
                    Metadata = new Dictionary<string, object>()
                };

                // 1. Estimate input tokens
                breakdown.InputTokens = await EstimateTextTokensAsync(input ?? "");

                // 2. Estimate system prompt tokens
                breakdown.SystemPromptTokens = await EstimateTextTokensAsync(systemPrompt ?? "");

                // 3. Estimate conversation history tokens
                breakdown.HistoryTokens = await EstimateHistoryTokensAsync(history ?? new List<string>());

                // 4. Calculate total input tokens
                breakdown.TotalInputTokens = breakdown.InputTokens + breakdown.SystemPromptTokens + breakdown.HistoryTokens;

                // 5. Estimate response tokens (25% of input or minimum 150)
                breakdown.EstimatedResponseTokens = Math.Max(150, (int)(breakdown.TotalInputTokens * 0.25));

                // 6. Calculate total estimated tokens
                breakdown.TotalEstimatedTokens = breakdown.TotalInputTokens + breakdown.EstimatedResponseTokens;

                // 7. Add safety buffer (10%)
                breakdown.SafetyBuffer = (int)(breakdown.TotalEstimatedTokens * 0.1);
                breakdown.TotalWithBuffer = breakdown.TotalEstimatedTokens + breakdown.SafetyBuffer;

                // 8. Determine model recommendations
                await AddModelRecommendationsAsync(breakdown);

                // 9. Add optimization suggestions
                AddOptimizationSuggestions(breakdown, input, systemPrompt, history);

                // 10. Cache result for performance
                await CacheTokenEstimationAsync(input, systemPrompt, history, breakdown);

                _logger.LogInformation("Token estimation completed. Total estimated: {TotalTokens}, With buffer: {WithBuffer}",
                    breakdown.TotalEstimatedTokens, breakdown.TotalWithBuffer);

                return breakdown;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating token usage");

                // Return conservative fallback estimation
                return CreateFallbackTokenBreakdown(input, systemPrompt, history);
            }
        }

        public async Task<bool> IsWithinTokenLimitAsync(string content, int maxTokens)
        {
            try
            {
                _logger.LogDebug("Checking if content is within token limit: {MaxTokens}", maxTokens);

                if (string.IsNullOrEmpty(content))
                    return true;

                // Quick estimation first
                var quickEstimate = EstimateTokensQuick(content);
                if (quickEstimate <= maxTokens)
                    return true;

                // If quick estimate exceeds, do more accurate count
                var accurateCount = await GetAccurateTokenCountAsync(content);
                var isWithinLimit = accurateCount <= maxTokens;

                _logger.LogDebug("Token limit check: {ActualTokens}/{MaxTokens} = {IsWithinLimit}",
                    accurateCount, maxTokens, isWithinLimit);

                return isWithinLimit;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token limit for content length: {ContentLength}", content?.Length ?? 0);

                // Conservative approach - assume it exceeds limit on error
                return false;
            }
        }

        // Additional helper methods for comprehensive token management
        public async Task<OptimizedContent> OptimizeContentForTokenLimitAsync(string content, int maxTokens, string optimizationStrategy = "intelligent")
        {
            try
            {
                _logger.LogInformation("Optimizing content for token limit: {MaxTokens}, Strategy: {Strategy}",
                    maxTokens, optimizationStrategy);

                var currentTokens = await GetAccurateTokenCountAsync(content);
                
                if (currentTokens <= maxTokens)
                {
                    return new OptimizedContent
                    {
                        OptimizedText = content,
                        OriginalTokenCount = currentTokens,
                        OptimizedTokenCount = currentTokens,
                        TokensSaved = 0,
                        OptimizationApplied = "none",
                        WasOptimized = false
                    };
                }

                var optimized = optimizationStrategy.ToLower() switch
                {
                    "aggressive" => await ApplyAggressiveOptimization(content, maxTokens),
                    "conservative" => await ApplyConservativeOptimization(content, maxTokens),
                    "intelligent" => await ApplyIntelligentOptimization(content, maxTokens),
                    _ => await ApplyIntelligentOptimization(content, maxTokens)
                };

                _logger.LogInformation("Content optimization completed. Tokens reduced from {OriginalTokens} to {OptimizedTokens}",
                    currentTokens, optimized.OptimizedTokenCount);

                return optimized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing content for token limit");

                // Fallback to simple truncation
                return await ApplySimpleTruncation(content, maxTokens);
            }
        }

        public async Task<List<ChatBox.API.Payload.Response.AIServiceResponse.TokenUsageStats>> GetTokenUsageStatsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var messageRepo = _unitOfWork.GetRepository<ChatMessage>();
                var messages = await messageRepo.GetListAsync(predicate: m =>
                    m.UserId == userId &&
                    (!fromDate.HasValue || m.CreatedAt >= fromDate.Value) &&
                    (!toDate.HasValue || m.CreatedAt <= toDate.Value) &&
                    !m.IsDeleted);

                var stats = messages.GroupBy(m => m.CreatedAt.Date)
                    .Select(g => new ChatBox.API.Payload.Response.AIServiceResponse.TokenUsageStats
                    {
                        Date = g.Key,
                        TotalTokensUsed = g.Sum(m => m.TokensUsed),
                        MessageCount = g.Count(),
                        AverageTokensPerMessage = g.Average(m => m.TokensUsed),
                        MaxTokensInSingleMessage = g.Max(m => m.TokensUsed),
                        EstimatedCost = CalculateEstimatedCost(g.Sum(m => m.TokensUsed))
                    })
                    .OrderBy(s => s.Date)
                    .ToList();

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token usage stats for user {UserId}", userId);
                return new List<ChatBox.API.Payload.Response.AIServiceResponse.TokenUsageStats>();
            }
        }

        // Private helper methods
        private async Task<int> EstimateTextTokensAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Check cache first
            var cacheKey = $"token_estimate:{text.GetHashCode()}";
            if (_memoryCache.TryGetValue(cacheKey, out int cachedCount))
            {
                return cachedCount;
            }

            try
            {
                // Try accurate count from AI service first
                var accurateCount = await _aiServiceClient.CountTokensAsync(text);
                if (accurateCount > 0)
                {
                    _memoryCache.Set(cacheKey, accurateCount, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                    return accurateCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "AI service token count failed, using estimation");
            }

            // Fallback to estimation
            var estimatedCount = EstimateTokensQuick(text);
            _memoryCache.Set(cacheKey, estimatedCount, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            return estimatedCount;
        }

        private async Task<int> EstimateHistoryTokensAsync(List<string> history)
        {
            if (history == null || !history.Any())
                return 0;

            var totalTokens = 0;
            foreach (var item in history)
            {
                totalTokens += await EstimateTextTokensAsync(item);
                // Add small overhead for conversation formatting
                totalTokens += 5;
            }

            return totalTokens;
        }

        private int EstimateTokensQuick(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Multiple estimation methods for accuracy
            var charBasedEstimate = (int)Math.Ceiling(text.Length / AVERAGE_CHARS_PER_TOKEN);
            var wordBasedEstimate = (int)Math.Ceiling(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * WORD_TO_TOKEN_RATIO);
            
            // Special handling for code and structured content
            var codeAdjustment = CalculateCodeAdjustment(text);
            var punctuationAdjustment = CalculatePunctuationAdjustment(text);

            // Use the higher estimate for safety, then apply adjustments
            var baseEstimate = Math.Max(charBasedEstimate, wordBasedEstimate);
            var adjustedEstimate = (int)(baseEstimate * (1 + codeAdjustment + punctuationAdjustment));

            return Math.Max(1, adjustedEstimate);
        }

        private double CalculateCodeAdjustment(string text)
        {
            // Code generally has higher token density
            var codePatterns = new[]
            {
                @"\{[^}]*\}",  // Curly braces
                @"\([^)]*\)",  // Parentheses
                @"\[[^\]]*\]", // Square brackets
                @"[a-zA-Z_][a-zA-Z0-9_]*\s*\(",  // Function calls
                @"(public|private|protected|class|interface|namespace)" // Keywords
            };

            var codeScore = 0;
            foreach (var pattern in codePatterns)
            {
                codeScore += Regex.Matches(text, pattern).Count;
            }

            // Return adjustment factor (0-0.3 for 0-30% increase)
            return Math.Min(0.3, codeScore / (double)text.Length * 10);
        }

        private double CalculatePunctuationAdjustment(string text)
        {
            // Heavy punctuation can increase token count
            var punctuationCount = text.Count(c => char.IsPunctuation(c));
            var punctuationRatio = punctuationCount / (double)text.Length;

            // Return adjustment factor (0-0.2 for 0-20% increase)
            return Math.Min(0.2, punctuationRatio * 2);
        }

        private async Task<int> GetAccurateTokenCountAsync(string content)
        {
            try
            {
                // Try AI service for accurate count
                var count = await _aiServiceClient.CountTokensAsync(content);
                return count > 0 ? count : EstimateTokensQuick(content);
            }
            catch
            {
                return EstimateTokensQuick(content);
            }
        }

        private async Task AddModelRecommendationsAsync(TokenBreakdown breakdown)
        {
            breakdown.ModelRecommendations = new List<ModelRecommendation>();

            foreach (var model in ModelTokenLimits)
            {
                var recommendation = new ModelRecommendation
                {
                    ModelName = model.Key,
                    MaxTokens = model.Value,
                    CanAccommodate = breakdown.TotalWithBuffer <= model.Value,
                    UtilizationPercentage = (double)breakdown.TotalWithBuffer / model.Value * 100,
                    RecommendationScore = CalculateRecommendationScore(breakdown.TotalWithBuffer, model.Value)
                };

                if (recommendation.CanAccommodate)
                {
                    recommendation.EstimatedCost = CalculateEstimatedCost(breakdown.TotalEstimatedTokens, model.Key);
                }

                breakdown.ModelRecommendations.Add(recommendation);
            }

            // Sort by recommendation score
            breakdown.ModelRecommendations = breakdown.ModelRecommendations
                .OrderByDescending(r => r.RecommendationScore)
                .ToList();
        }

        private void AddOptimizationSuggestions(TokenBreakdown breakdown, string input, string systemPrompt, List<string> history)
        {
            breakdown.OptimizationSuggestions = new List<string>();

            // Check if optimization is needed
            if (breakdown.TotalWithBuffer < 1000)
            {
                breakdown.OptimizationSuggestions.Add("Token usage is efficient - no optimization needed");
                return;
            }

            // History optimization
            if (breakdown.HistoryTokens > breakdown.TotalInputTokens * 0.5)
            {
                breakdown.OptimizationSuggestions.Add("Consider limiting conversation history to recent messages");
                breakdown.OptimizationSuggestions.Add($"History uses {breakdown.HistoryTokens} tokens ({breakdown.HistoryTokens * 100.0 / breakdown.TotalInputTokens:F1}% of input)");
            }

            // System prompt optimization
            if (breakdown.SystemPromptTokens > 500)
            {
                breakdown.OptimizationSuggestions.Add("System prompt is quite long - consider condensing instructions");
            }

            // Input optimization
            if (breakdown.InputTokens > 2000)
            {
                breakdown.OptimizationSuggestions.Add("Input message is lengthy - consider breaking into smaller parts");
            }

            // General suggestions
            if (breakdown.TotalWithBuffer > 8000)
            {
                breakdown.OptimizationSuggestions.Add("Consider using a model with higher token limits");
                breakdown.OptimizationSuggestions.Add("Implement content chunking for large inputs");
            }
        }

        private async Task CacheTokenEstimationAsync(string input, string systemPrompt, List<string> history, TokenBreakdown breakdown)
        {
            try
            {
                var cacheKey = GenerateCacheKey(input, systemPrompt, history);
                _memoryCache.Set(cacheKey, breakdown, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error caching token estimation");
            }
        }

        private string GenerateCacheKey(string input, string systemPrompt, List<string> history)
        {
            var combined = $"{input}|{systemPrompt}|{string.Join("|", history ?? new List<string>())}";
            return $"token_breakdown:{combined.GetHashCode()}";
        }

        private TokenBreakdown CreateFallbackTokenBreakdown(string input, string systemPrompt, List<string> history)
        {
            var inputTokens = EstimateTokensQuick(input ?? "");
            var systemTokens = EstimateTokensQuick(systemPrompt ?? "");
            var historyTokens = (history?.Sum(h => EstimateTokensQuick(h)) ?? 0) + (history?.Count ?? 0) * 5;

            var totalInput = inputTokens + systemTokens + historyTokens;
            var estimatedResponse = Math.Max(150, (int)(totalInput * 0.25));
            var total = totalInput + estimatedResponse;
            var buffer = (int)(total * 0.1);

            return new TokenBreakdown
            {
                InputTokens = inputTokens,
                SystemPromptTokens = systemTokens,
                HistoryTokens = historyTokens,
                TotalInputTokens = totalInput,
                EstimatedResponseTokens = estimatedResponse,
                TotalEstimatedTokens = total,
                SafetyBuffer = buffer,
                TotalWithBuffer = total + buffer,
                EstimationTimestamp = DateTime.UtcNow,
                EstimationMethod = "fallback_estimation",
                ModelRecommendations = new List<ModelRecommendation>(),
                OptimizationSuggestions = new List<string> { "Estimation failed - using conservative fallback" },
                Metadata = new Dictionary<string, object>()
            };
        }

        private async Task<OptimizedContent> ApplyIntelligentOptimization(string content, int maxTokens)
        {
            var originalTokens = await GetAccurateTokenCountAsync(content);
            var targetTokens = (int)(maxTokens * 0.95); // Leave 5% buffer
            
            if (originalTokens <= targetTokens)
            {
                return new OptimizedContent
                {
                    OptimizedText = content,
                    OriginalTokenCount = originalTokens,
                    OptimizedTokenCount = originalTokens,
                    TokensSaved = 0,
                    OptimizationApplied = "none",
                    WasOptimized = false
                };
            }

            var optimizedContent = content;
            var optimizations = new List<string>();

            // 1. Remove extra whitespace
            optimizedContent = Regex.Replace(optimizedContent, @"\s+", " ");
            optimizations.Add("whitespace_normalization");

            // 2. Remove redundant phrases
            optimizedContent = RemoveRedundantPhrases(optimizedContent);
            optimizations.Add("redundancy_removal");

            // 3. Simplify complex sentences
            optimizedContent = SimplifySentences(optimizedContent);
            optimizations.Add("sentence_simplification");

            // 4. If still too long, apply intelligent truncation
            var currentTokens = await GetAccurateTokenCountAsync(optimizedContent);
            if (currentTokens > targetTokens)
            {
                optimizedContent = await _aiServiceClient.TruncateToTokenLimitAsync(optimizedContent, targetTokens);
                optimizations.Add("intelligent_truncation");
            }

            var finalTokens = await GetAccurateTokenCountAsync(optimizedContent);

            return new OptimizedContent
            {
                OptimizedText = optimizedContent,
                OriginalTokenCount = originalTokens,
                OptimizedTokenCount = finalTokens,
                TokensSaved = originalTokens - finalTokens,
                OptimizationApplied = string.Join(", ", optimizations),
                WasOptimized = true
            };
        }

        private async Task<OptimizedContent> ApplyConservativeOptimization(string content, int maxTokens)
        {
            var originalTokens = await GetAccurateTokenCountAsync(content);
            var targetTokens = (int)(maxTokens * 0.9); // Leave 10% buffer
            
            if (originalTokens <= targetTokens)
            {
                return new OptimizedContent
                {
                    OptimizedText = content,
                    OriginalTokenCount = originalTokens,
                    OptimizedTokenCount = originalTokens,
                    TokensSaved = 0,
                    OptimizationApplied = "none",
                    WasOptimized = false
                };
            }

            // Conservative approach - only remove whitespace and apply gentle truncation
            var optimizedContent = Regex.Replace(content, @"\s+", " ").Trim();
            
            var currentTokens = await GetAccurateTokenCountAsync(optimizedContent);
            if (currentTokens > targetTokens)
            {
                // Simple truncation at sentence boundaries
                var sentences = optimizedContent.Split('.', StringSplitOptions.RemoveEmptyEntries);
                var truncatedSentences = new List<string>();
                var runningTokens = 0;

                foreach (var sentence in sentences)
                {
                    var sentenceTokens = EstimateTokensQuick(sentence + ".");
                    if (runningTokens + sentenceTokens <= targetTokens)
                    {
                        truncatedSentences.Add(sentence.Trim());
                        runningTokens += sentenceTokens;
                    }
                    else
                    {
                        break;
                    }
                }

                optimizedContent = string.Join(". ", truncatedSentences) + ".";
            }

            var finalTokens = await GetAccurateTokenCountAsync(optimizedContent);

            return new OptimizedContent
            {
                OptimizedText = optimizedContent,
                OriginalTokenCount = originalTokens,
                OptimizedTokenCount = finalTokens,
                TokensSaved = originalTokens - finalTokens,
                OptimizationApplied = "conservative_optimization",
                WasOptimized = true
            };
        }

        private async Task<OptimizedContent> ApplyAggressiveOptimization(string content, int maxTokens)
        {
            var originalTokens = await GetAccurateTokenCountAsync(content);
            var targetTokens = (int)(maxTokens * 0.85); // Leave 15% buffer
            
            var optimizedContent = content;
            var optimizations = new List<string>();

            // 1. Remove all extra whitespace
            optimizedContent = Regex.Replace(optimizedContent, @"\s+", " ").Trim();
            optimizations.Add("aggressive_whitespace_removal");

            // 2. Remove filler words
            optimizedContent = RemoveFillerWords(optimizedContent);
            optimizations.Add("filler_word_removal");

            // 3. Abbreviate common phrases
            optimizedContent = AbbreviateCommonPhrases(optimizedContent);
            optimizations.Add("phrase_abbreviation");

            // 4. Remove redundant information
            optimizedContent = RemoveRedundantInformation(optimizedContent);
            optimizations.Add("redundancy_removal");

            // 5. If still too long, aggressive truncation
            var currentTokens = await GetAccurateTokenCountAsync(optimizedContent);
            if (currentTokens > targetTokens)
            {
                var targetLength = (int)(optimizedContent.Length * ((double)targetTokens / currentTokens));
                optimizedContent = optimizedContent.Substring(0, Math.Min(targetLength, optimizedContent.Length));
                optimizations.Add("aggressive_truncation");
            }

            var finalTokens = await GetAccurateTokenCountAsync(optimizedContent);

            return new OptimizedContent
            {
                OptimizedText = optimizedContent,
                OriginalTokenCount = originalTokens,
                OptimizedTokenCount = finalTokens,
                TokensSaved = originalTokens - finalTokens,
                OptimizationApplied = string.Join(", ", optimizations),
                WasOptimized = true
            };
        }

        private async Task<OptimizedContent> ApplySimpleTruncation(string content, int maxTokens)
        {
            var originalTokens = await GetAccurateTokenCountAsync(content);
            var targetTokens = (int)(maxTokens * 0.9);

            if (originalTokens <= targetTokens)
            {
                return new OptimizedContent
                {
                    OptimizedText = content,
                    OriginalTokenCount = originalTokens,
                    OptimizedTokenCount = originalTokens,
                    TokensSaved = 0,
                    OptimizationApplied = "none",
                    WasOptimized = false
                };
            }

            // Simple character-based truncation with ellipsis
            var targetLength = (int)(content.Length * ((double)targetTokens / originalTokens));
            var truncatedContent = content.Substring(0, Math.Min(targetLength - 3, content.Length)) + "...";
            
            var finalTokens = await GetAccurateTokenCountAsync(truncatedContent);

            return new OptimizedContent
            {
                OptimizedText = truncatedContent,
                OriginalTokenCount = originalTokens,
                OptimizedTokenCount = finalTokens,
                TokensSaved = originalTokens - finalTokens,
                OptimizationApplied = "simple_truncation",
                WasOptimized = true
            };
        }

        private string RemoveRedundantPhrases(string content)
        {
            var redundantPhrases = new[]
            {
                @"\b(in other words|that is to say|to put it another way),?\s*",
                @"\b(as I mentioned|as mentioned earlier|as stated before),?\s*",
                @"\b(obviously|clearly|evidently|of course),?\s*",
                @"\b(basically|essentially|fundamentally),?\s*"
            };

            foreach (var pattern in redundantPhrases)
            {
                content = Regex.Replace(content, pattern, "", RegexOptions.IgnoreCase);
            }

            return content;
        }

        private string SimplifySentences(string content)
        {
            // Replace complex constructions with simpler ones
            var simplifications = new Dictionary<string, string>
            {
                { @"\bin order to\b", "to" },
                { @"\bdue to the fact that\b", "because" },
                { @"\bat this point in time\b", "now" },
                { @"\bfor the purpose of\b", "to" },
                { @"\bin the event that\b", "if" }
            };

            foreach (var simplification in simplifications)
            {
                content = Regex.Replace(content, simplification.Key, simplification.Value, RegexOptions.IgnoreCase);
            }

            return content;
        }

        private string RemoveFillerWords(string content)
        {
            var fillerWords = new[]
            {
                @"\b(um|uh|like|you know|actually|literally|basically|totally|really|very|quite|rather|pretty),?\s*"
            };

            foreach (var pattern in fillerWords)
            {
                content = Regex.Replace(content, pattern, "", RegexOptions.IgnoreCase);
            }

            return content;
        }

        private string AbbreviateCommonPhrases(string content)
        {
            var abbreviations = new Dictionary<string, string>
            {
                { @"\bfor example\b", "e.g." },
                { @"\bthat is\b", "i.e." },
                { @"\band so on\b", "etc." },
                { @"\band others\b", "et al." },
                { @"\bwith respect to\b", "re:" },
                { @"\bwith regard to\b", "re:" }
            };

            foreach (var abbreviation in abbreviations)
            {
                content = Regex.Replace(content, abbreviation.Key, abbreviation.Value, RegexOptions.IgnoreCase);
            }

            return content;
        }

        private string RemoveRedundantInformation(string content)
        {
            // Remove repeated information patterns
            var sentences = content.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var uniqueSentences = new List<string>();
            var seenConcepts = new HashSet<string>();

            foreach (var sentence in sentences)
            {
                var normalizedSentence = sentence.Trim().ToLower();
                var words = normalizedSentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var key = string.Join(" ", words.Take(5)); // Use first 5 words as key

                if (!seenConcepts.Contains(key))
                {
                    uniqueSentences.Add(sentence.Trim());
                    seenConcepts.Add(key);
                }
            }

            return string.Join(". ", uniqueSentences) + ".";
        }

        private double CalculateRecommendationScore(int requiredTokens, int modelLimit)
        {
            if (requiredTokens > modelLimit)
                return 0.0;

            var utilization = (double)requiredTokens / modelLimit;
            
            // Optimal utilization is around 60-80%
            if (utilization >= 0.6 && utilization <= 0.8)
                return 1.0;
            
            if (utilization < 0.6)
                return 0.8 - (0.6 - utilization); // Penalize under-utilization
            
            return 1.2 - utilization; // Penalize over-utilization
        }

        private double CalculateEstimatedCost(int tokens, string model = "default")
        {
            // Rough cost estimates (per 1K tokens) - would be configurable in production
            var costPer1K = model.ToLower() switch
            {
                "gpt-4" => 0.03,
                "gpt-4-turbo" => 0.01,
                "gpt-3.5-turbo" => 0.002,
                "claude-3-opus" => 0.015,
                "claude-3-sonnet" => 0.003,
                _ => 0.002
            };

            return (tokens / 1000.0) * costPer1K;
        }
    }
} 