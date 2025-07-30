

using ChatBox.API.Services.Interfaces;
using SharpToken;

namespace ChatBox.API.Services.Implement
{ 
    public class TokenCountService : ITokenCountService
        {
            private readonly IConfiguration _configuration;
            private readonly GptEncoding _encoding;

            public TokenCountService(IConfiguration configuration)
            {
                _configuration = configuration;
                var tokenizerModel = _configuration["ChatService:TokenizerModel"];

                // Tạo encoding cho Mistral sử dụng SharpToken
                _encoding = CreateEncodingForMistral(tokenizerModel);
            }

            private GptEncoding CreateEncodingForMistral(string modelName)
            {


                if (IsMistralModel(modelName))
                {
                    return GptEncoding.GetEncoding("cl100k_base");
                }

                // Fallback cho các model khác
                return GptEncoding.GetEncoding("cl100k_base");
            }

            private bool IsMistralModel(string modelName)
            {
                return modelName?.Contains("mistral", StringComparison.OrdinalIgnoreCase) == true;
            }

            public int CountTokens(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return 0;

                try
                {
                    // Encode text thành tokens bằng SharpToken
                    var tokens = _encoding.Encode(text);

                    // Điều chỉnh cho Mistral models
                    var tokenizerModel = _configuration["ChatService:TokenizerModel"];

                    if (IsMistralModel(tokenizerModel))
                    {
                        // Mistral tokenizer thường tạo ra ít token hơn GPT-4 khoảng 5-10%
                        // Dựa trên research, giảm 8% để gần với Mistral tokenizer thực tế
                        return Math.Max(1, (int)(tokens.Count * 0.92));
                    }

                    return tokens.Count;
                }
                catch (Exception)
                {
                    // Fallback: ước tính đơn giản
                    return EstimateTokenCount(text);
                }
            }

            private int EstimateTokenCount(string text)
            {
                // Ước tính cho Mistral: 3.5 ký tự = 1 token
                return Math.Max(1, (int)Math.Ceiling(text.Length / 3.5));
            }

            public bool IsWithinLimit(string text, int? maxTokens = null)
            {
                var limit = maxTokens ?? _configuration.GetValue<int>("ChatService:DefaultMaxTokens");
                return CountTokens(text) <= limit;
            }

        }
    }

