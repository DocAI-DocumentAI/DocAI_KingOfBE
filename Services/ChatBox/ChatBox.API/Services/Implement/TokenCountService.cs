

using ChatBox.API.Constants;
using ChatBox.API.Services.Interfaces;
using MassTransit.Courier;
using Microsoft.SemanticKernel.ChatCompletion;
using SharpToken;

namespace ChatBox.API.Services.Implement
{
    public class TokenCountService : ITokenCountService
    {
        private readonly IConfiguration _configuration;
        private readonly GptEncoding _encoding;
        private readonly ILogger<TokenCountService> _logger; 

        public TokenCountService(IConfiguration configuration, ILogger<TokenCountService> logger) 
        {
            _configuration = configuration;
            _logger = logger; // 🔧 ASSIGN
            _encoding = CreateEncodingForMistral(ChatConstants.TokenizerModel);
        }

        private GptEncoding CreateEncodingForMistral(string modelName)
        {
            if (IsMistralModel(modelName))
            {
                return GptEncoding.GetEncoding(ChatConstants.DefaultEncodingName);
            }
            return GptEncoding.GetEncoding(ChatConstants.DefaultEncodingName);
        }

        private bool IsMistralModel(string modelName)
        {
            return !string.IsNullOrEmpty(modelName) &&
                   modelName.Contains("mistral", StringComparison.OrdinalIgnoreCase);
        }

        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            try
            {
                var tokens = _encoding.Encode(text);
                var count = tokens.Count;
                if (IsMistralModel(ChatConstants.TokenizerModel))
                {
                    count = (int)(count * ChatConstants.MistralTokenAdjustment);
                }
                return Math.Max(1, count);
            }
            catch (Exception ex)
            {
                // 🔧 FIX: Thêm logging khi fallback
                _logger.LogWarning(ex, "Token counting failed for text length {Length}, using estimation fallback", text.Length);
                return EstimateTokenCount(text);
            }
        }

        // ... rest of methods remain the same
        private int EstimateTokenCount(string text)
        {
            return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
        }

        public bool IsWithinLimit(string text, int? maxTokens = null)
        {
            var limit = maxTokens ?? ChatConstants.DefaultMaxTokens;
            return CountTokens(text) <= limit;
        }

        public int GetMaxTokensForModel(string modelName)
        {
            return modelName.ToLower() switch
            {
                var name when name.Contains("gpt-4") => ChatConstants.GPT4MaxTokens,
                var name when name.Contains("gpt-3.5") => ChatConstants.GPT35MaxTokens,
                var name when name.Contains("mistral") => ChatConstants.MistralMaxTokens,
                _ => ChatConstants.DefaultMaxTokens
            };
        }

        public int EstimateContextTokens(ChatHistory chatHistory)
        {
            var totalTokens = 0;
            foreach (var message in chatHistory)
            {
                totalTokens += CountTokens(message.Content ?? "");
            }
            return totalTokens;
        }

        public bool IsContextWithinLimit(ChatHistory chatHistory, string modelName)
        {
            var maxContextTokens = GetMaxContextTokensForModel(modelName);
            var currentTokens = EstimateContextTokens(chatHistory);
            return currentTokens <= maxContextTokens;
        }

        private int GetMaxContextTokensForModel(string modelName)
        {
            return modelName.ToLower() switch
            {
                var name when name.Contains("mistral") => ChatConstants.MistralMaxContextTokens,
                var name when name.Contains("gpt-4") => ChatConstants.GPT4MaxContextTokens,
                var name when name.Contains("gpt-3.5") => ChatConstants.GPT35MaxContextTokens,
                _ => ChatConstants.DefaultMaxContextTokens
            };
        }
    }
}

