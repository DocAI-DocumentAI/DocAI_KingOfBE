

using ChatBox.API.Services.Interfaces;
using Tiktoken;

namespace ChatBox.API.Services.Implement
{
    public class TokenCountService : ITokenCountService
    {
        private readonly IConfiguration _configuration;
        private readonly Encoder _tokenizer;
        public TokenCountService(IConfiguration configuration)
        {
            _configuration = configuration;
            var tokenizerModel = _configuration["ChatService:TokenizerModel"];
            _tokenizer = ModelToEncoder.For(tokenizerModel);
        }

        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var tokens = _tokenizer.Encode(text);
            return tokens.Count;
        }

        public bool IsWithinLimit(string text, int? maxTokens = null)
        {
            var limit = maxTokens ?? _configuration.GetValue<int>("ChatService:DefaultMaxTokens");
            return CountTokens(text) <= limit;
        }
    }
}
