

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
        private readonly IConfiguration _configuration;
        private readonly ILogger<TokenCountService> _logger;

        // 🔧 Cache tokenizers để tránh tải lại nhiều lần
        private readonly ConcurrentDictionary<string, Tokenizer> _tokenizerCache = new();

        // 🔧 Fallback tokenizer (CL100K)
        private readonly Lazy<Tokenizer> _fallbackTokenizer;

        public TokenCountService(IConfiguration configuration, ILogger<TokenCountService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // 🔧 Initialize fallback tokenizer (CL100K/GPT-4 style)
            _fallbackTokenizer = new Lazy<Tokenizer>(() =>
            {
                try
                {
                    // Microsoft.ML.Tokenizers equivalent of CL100K
                    return TiktokenTokenizer.CreateForModel("gpt-4");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create fallback tokenizer, using basic estimation");
                    return null;
                }
            });
        }

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
                    var count = tokens.Count;

                    // 🔧 Apply model-specific adjustments
                    count = ApplyModelTokenAdjustment(count, modelName);

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

            // 🔧 Fallback to estimation
            return EstimateTokenCount(text);
        }

        public int CountTokens(string text) => CountTokens(text, null);

        private Tokenizer GetTokenizerForModel(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return _fallbackTokenizer.Value;

            // 🔧 Check cache first
            if (_tokenizerCache.TryGetValue(modelName, out var cachedTokenizer))
                return cachedTokenizer;

            try
            {
                var tokenizer = CreateTokenizerForModel(modelName);
                if (tokenizer != null)
                {
                    // 🔧 Cache successful tokenizer
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
                // 🔧 DeepSeek models - use LLaMA 2 tokenizer
                if (normalizedName.Contains("deepseek"))
                {
                    _logger.LogDebug("Creating LLaMA tokenizer for DeepSeek model: {ModelName}", modelName);
                    return TiktokenTokenizer.CreateForModel("gpt-3.5-turbo"); // Closest equivalent
                }

                // 🔧 LLaMA 3.3 models - use LLaMA 3 tokenizer  
                if (normalizedName.Contains("llama-3") || normalizedName.Contains("llama3"))
                {
                    _logger.LogDebug("Creating LLaMA 3 tokenizer for model: {ModelName}", modelName);
                    // LLaMA 3 uses different tokenizer than LLaMA 2
                    return TiktokenTokenizer.CreateForModel("gpt-4"); // Better approximation for LLaMA 3
                }

                // 🔧 General LLaMA models (LLaMA 2 style)
                if (normalizedName.Contains("llama") && !normalizedName.Contains("llama-3"))
                {
                    _logger.LogDebug("Creating LLaMA 2 tokenizer for model: {ModelName}", modelName);
                    return TiktokenTokenizer.CreateForModel("gpt-3.5-turbo");
                }

                // 🔧 Mistral models - use Mistral-specific approach
                if (normalizedName.Contains("mistral"))
                {
                    _logger.LogDebug("Creating Mistral tokenizer for model: {ModelName}", modelName);
                    // Mistral tokenizer approximation
                    return TiktokenTokenizer.CreateForModel("gpt-3.5-turbo"); // Reasonable approximation
                }

                // 🔧 GPT models - use appropriate Tiktoken
                if (normalizedName.Contains("gpt-4"))
                {
                    return TiktokenTokenizer.CreateForModel("gpt-4");
                }

                if (normalizedName.Contains("gpt-3.5"))
                {
                    return TiktokenTokenizer.CreateForModel("gpt-3.5-turbo");
                }

                // 🔧 Claude models - approximate with GPT-4
                if (normalizedName.Contains("claude"))
                {
                    return TiktokenTokenizer.CreateForModel("gpt-4");
                }

                // 🔧 Default fallback
                _logger.LogDebug("Using fallback tokenizer for unknown model: {ModelName}", modelName);
                return _fallbackTokenizer.Value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception creating specific tokenizer for {ModelName}", modelName);
                return _fallbackTokenizer.Value;
            }
        }

        private int ApplyModelTokenAdjustment(int baseCount, string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return baseCount;

            var normalizedName = modelName.ToLowerInvariant();

            // 🔧 Model-specific token adjustments
            var adjustment = normalizedName switch
            {
                var name when name.Contains("mistral") => ChatConstants.MistralTokenAdjustment,
                var name when name.Contains("deepseek") => 1.0, // No adjustment needed
                var name when name.Contains("llama") => 1.0, // No adjustment needed  
                _ => 1.0
            };

            return (int)(baseCount * adjustment);
        }

        private int EstimateTokenCount(string text)
        {
            // 🔧 Improved estimation based on text characteristics
            if (string.IsNullOrEmpty(text)) return 0;

            // Basic estimation: ~4 characters per token for English/Vietnamese
            var baseEstimate = Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));

            // 🔧 Adjust for Vietnamese text (tends to have more tokens)
            var vietnameseChars = text.Count(c => c > 127); // Non-ASCII chars
            if (vietnameseChars > text.Length * 0.3) // > 30% non-ASCII
            {
                baseEstimate = (int)(baseEstimate * 1.2); // 20% more tokens for Vietnamese
            }

            return baseEstimate;
        }

        // 🔧 Enhanced methods với model-aware functionality
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

            return modelName.ToLowerInvariant() switch
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
        public int EstimateContextTokens(ChatHistory chatHistory) => EstimateContextTokens(chatHistory, null);

        public int EstimateContextTokens(ChatHistory chatHistory, string modelName = null)
        {
            var totalTokens = 0;
            foreach (var message in chatHistory)
            {
                totalTokens += CountTokens(message.Content ?? "", modelName);
            }
            return totalTokens;
        }

        public bool IsContextWithinLimit(ChatHistory chatHistory, string modelName)
        {
            var maxContextTokens = GetMaxContextTokensForModel(modelName);
            var currentTokens = EstimateContextTokens(chatHistory, modelName);
            return currentTokens <= maxContextTokens;
        }

        private int GetMaxContextTokensForModel(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return ChatConstants.DefaultMaxContextTokens;

            return modelName.ToLowerInvariant() switch
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

        // 🔧 Cleanup method to prevent memory leaks
        public void ClearTokenizerCache()
        {
            _tokenizerCache.Clear();
            _logger.LogInformation("Tokenizer cache cleared");
        }

        // 🔧 Get cache statistics for monitoring
        public TokenizerCacheStats GetCacheStats()
        {
            return new TokenizerCacheStats
            {
                CachedTokenizerCount = _tokenizerCache.Count,
                CachedModels = _tokenizerCache.Keys.ToList()
            };
        }
    }

        public class TokenizerCacheStats
    {
        public int CachedTokenizerCount { get; set; }
        public List<string> CachedModels { get; set; } = new();
    }
}
