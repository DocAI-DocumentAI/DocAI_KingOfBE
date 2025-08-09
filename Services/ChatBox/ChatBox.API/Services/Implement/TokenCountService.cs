

using System.Collections.Concurrent;
using ChatBox.API.Constants;
using ChatBox.API.Services.Interfaces;
using MassTransit.Courier;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel.ChatCompletion;
using SharpToken;

namespace ChatBox.API.Services.Implement
{
    public class TokenCountService : ITokenCountService
    {
        #region Fields
        private readonly IConfiguration _configuration;
        private readonly ILogger<TokenCountService> _logger;

        // Cache tokenizers to avoid reloading multiple times
        private readonly ConcurrentDictionary<string, Tokenizer> _tokenizerCache = new();

        // Fallback tokenizer (CL100K/GPT-4 style)
        private readonly Lazy<Tokenizer> _fallbackTokenizer;
        #endregion

        #region Constructor
        public TokenCountService(IConfiguration configuration, ILogger<TokenCountService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _fallbackTokenizer = new Lazy<Tokenizer>(CreateFallbackTokenizer);
        }
        #endregion

        #region Public Methods
        public int CountTokens(string text, string modelName = null)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            try
            {
                var tokenizer = GetTokenizerForModel(modelName);
                if (tokenizer != null)
                {
                    var tokens = tokenizer.EncodeToTokens(text, out _);
                    var count = ApplyModelTokenAdjustment(tokens.Count, modelName);

                    _logger.LogDebug("Token count for model {ModelName}: {Count} tokens",
                        modelName ?? "fallback", count);

                    return Math.Max(1, count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token counting failed for model {ModelName}, using estimation fallback",
                    modelName ?? "unknown");
            }

            return EstimateTokenCount(text);
        }

        public int CountTokens(string text) => CountTokens(text, null);

        public bool IsWithinLimit(string text, int? maxTokens = null)
        {
            var limit = maxTokens ?? ChatConstants.DefaultMaxTokens;
            return CountTokens(text) <= limit;
        }

        public bool IsWithinLimit(string text, int? maxTokens, string modelName)
        {
            var limit = maxTokens ?? ChatConstants.DefaultMaxTokens;
            return CountTokens(text, modelName) <= limit;
        }

        public int GetMaxTokensForModel(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return ChatConstants.DefaultMaxTokens;

            return GetModelSpecificTokenLimit(modelName.ToLowerInvariant());
        }

        public int EstimateContextTokens(ChatHistory chatHistory) => EstimateContextTokens(chatHistory, null);

        public int EstimateContextTokens(ChatHistory chatHistory, string modelName = null)
        {
            return chatHistory.Sum(message => CountTokens(message.Content ?? "", modelName));
        }

        public bool IsContextWithinLimit(ChatHistory chatHistory, string modelName)
        {
            var maxContextTokens = GetMaxContextTokensForModel(modelName);
            var currentTokens = EstimateContextTokens(chatHistory, modelName);
            return currentTokens <= maxContextTokens;
        }

        public void ClearTokenizerCache()
        {
            _tokenizerCache.Clear();
            _logger.LogInformation("Tokenizer cache cleared");
        }

        public TokenizerCacheStats GetCacheStats()
        {
            return new TokenizerCacheStats
            {
                CachedTokenizerCount = _tokenizerCache.Count,
                CachedModels = _tokenizerCache.Keys.ToList()
            };
        }
        #endregion

        #region Private Methods - Tokenizer Management
        private Tokenizer CreateFallbackTokenizer()
        {
            try
            {
                return TiktokenTokenizer.CreateForModel("gpt-4");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create fallback tokenizer, using basic estimation");
                return null;
            }
        }

        private Tokenizer GetTokenizerForModel(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return _fallbackTokenizer.Value;

            if (_tokenizerCache.TryGetValue(modelName, out var cachedTokenizer))
                return cachedTokenizer;

            return CreateAndCacheTokenizer(modelName);
        }

        private Tokenizer CreateAndCacheTokenizer(string modelName)
        {
            try
            {
                var tokenizer = CreateTokenizerForModel(modelName);
                if (tokenizer != null)
                {
                    _tokenizerCache.TryAdd(modelName, tokenizer);
                    _logger.LogInformation("Created and cached tokenizer for model: {ModelName}", modelName);
                }
                return tokenizer;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create tokenizer for model {ModelName}, using fallback", modelName);
                return _fallbackTokenizer.Value;
            }
        }

        private Tokenizer CreateTokenizerForModel(string modelName)
        {
            var normalizedName = modelName.ToLowerInvariant();

            try
            {
                return normalizedName switch
                {
                    var name when name.Contains("deepseek") => CreateDeepSeekTokenizer(modelName),
                    var name when name.Contains("llama-3") || name.Contains("llama3") => CreateLlama3Tokenizer(modelName),
                    var name when name.Contains("llama") && !name.Contains("llama-3") => CreateLlama2Tokenizer(modelName),
                    var name when name.Contains("mistral") => CreateMistralTokenizer(modelName),
                    var name when name.Contains("gpt-4") => TiktokenTokenizer.CreateForModel("gpt-4"),
                    var name when name.Contains("gpt-3.5") => TiktokenTokenizer.CreateForModel("gpt-3.5-turbo"),
                    var name when name.Contains("claude") => TiktokenTokenizer.CreateForModel("gpt-4"), // Approximation
                    _ => CreateDefaultTokenizer(modelName)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception creating specific tokenizer for {ModelName}", modelName);
                return _fallbackTokenizer.Value;
            }
        }

        private Tokenizer CreateDeepSeekTokenizer(string modelName)
        {
            _logger.LogDebug("Creating LLaMA tokenizer for DeepSeek model: {ModelName}", modelName);
            return TiktokenTokenizer.CreateForModel("gpt-3.5-turbo"); // Closest equivalent
        }

        private Tokenizer CreateLlama3Tokenizer(string modelName)
        {
            _logger.LogDebug("Creating LLaMA 3 tokenizer for model: {ModelName}", modelName);
            return TiktokenTokenizer.CreateForModel("gpt-4"); // Better approximation for LLaMA 3
        }

        private Tokenizer CreateLlama2Tokenizer(string modelName)
        {
            _logger.LogDebug("Creating LLaMA 2 tokenizer for model: {ModelName}", modelName);
            return TiktokenTokenizer.CreateForModel("gpt-3.5-turbo");
        }

        private Tokenizer CreateMistralTokenizer(string modelName)
        {
            _logger.LogDebug("Creating Mistral tokenizer for model: {ModelName}", modelName);
            return TiktokenTokenizer.CreateForModel("gpt-3.5-turbo"); // Reasonable approximation
        }

        private Tokenizer CreateDefaultTokenizer(string modelName)
        {
            _logger.LogDebug("Using fallback tokenizer for unknown model: {ModelName}", modelName);
            return _fallbackTokenizer.Value;
        }
        #endregion

        #region Private Methods - Token Calculations
        private int ApplyModelTokenAdjustment(int baseCount, string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return baseCount;

            var adjustment = GetModelTokenAdjustment(modelName.ToLowerInvariant());
            return (int)(baseCount * adjustment);
        }

        private double GetModelTokenAdjustment(string normalizedModelName)
        {
            return normalizedModelName switch
            {
                var name when name.Contains("mistral") => ChatConstants.MistralTokenAdjustment,
                var name when name.Contains("deepseek") => 1.0,
                var name when name.Contains("llama") => 1.0,
                _ => 1.0
            };
        }

        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Basic estimation: ~4 characters per token for English/Vietnamese
            var baseEstimate = Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));

            // Adjust for Vietnamese text (tends to have more tokens)
            return ApplyVietnameseAdjustment(text, baseEstimate);
        }

        private int ApplyVietnameseAdjustment(string text, int baseEstimate)
        {
            var vietnameseChars = text.Count(c => c > 127); // Non-ASCII chars
            if (vietnameseChars > text.Length * 0.3) // > 30% non-ASCII
            {
                return (int)(baseEstimate * 1.2); // 20% more tokens for Vietnamese
            }
            return baseEstimate;
        }

        private int GetModelSpecificTokenLimit(string normalizedModelName)
        {
            return normalizedModelName switch
            {
                var name when name.Contains("gpt-4") => ChatConstants.GPT4MaxTokens,
                var name when name.Contains("gpt-3.5") => ChatConstants.GPT35MaxTokens,
                var name when name.Contains("mistral") => ChatConstants.MistralMaxTokens,
                var name when name.Contains("deepseek") => 32000, // DeepSeek-R1 supports long context
                var name when name.Contains("llama-3.3") => 131072, // LLaMA 3.3 has very long context
                var name when name.Contains("llama") => 4096, // General LLaMA models
                _ => ChatConstants.DefaultMaxTokens
            };
        }

        private int GetMaxContextTokensForModel(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return ChatConstants.DefaultMaxContextTokens;

            return GetModelSpecificContextLimit(modelName.ToLowerInvariant());
        }

        private int GetModelSpecificContextLimit(string normalizedModelName)
        {
            return normalizedModelName switch
            {
                var name when name.Contains("mistral") => ChatConstants.MistralMaxContextTokens,
                var name when name.Contains("gpt-4") => ChatConstants.GPT4MaxContextTokens,
                var name when name.Contains("gpt-3.5") => ChatConstants.GPT35MaxContextTokens,
                var name when name.Contains("deepseek") => 30000, // Conservative context for DeepSeek
                var name when name.Contains("llama-3.3") => 120000, // Very large context for LLaMA 3.3
                var name when name.Contains("llama") => 3500, // Conservative for general LLaMA
                _ => ChatConstants.DefaultMaxContextTokens
            };
        }
        #endregion
    }

    /// <summary>
    /// Statistics about the tokenizer cache
    /// </summary>
    public class TokenizerCacheStats
    {
        public int CachedTokenizerCount { get; set; }
        public List<string> CachedModels { get; set; } = new();
    }
}
