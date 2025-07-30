

using ChatBox.API.Services.Interfaces;
using Tiktoken;

namespace ChatBox.API.Services.Implement
{
    public class TokenCountService : ITokenCountService
    {
        private readonly Encoder _tokenizer;
        public TokenCountService()
        {
            _tokenizer = ModelToEncoder.For("gpt-4");
        }

        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var tokens = _tokenizer.Encode(text);
            return tokens.Count;
        }

        public bool IsWithinLimit(string text, int maxTokens = 4000)
        {
            return CountTokens(text) <= maxTokens;
        }
    }
}
