using ChatBox.API.Payload.Response.AIServiceResponse;
using ChatBox.API.Payload.Response;
using ChatBox.API.Payload.Response.ChatServiceResponse;

namespace ChatBox.API.Services.Interfaces
{
    public interface ITokenValidationService
    {
        Task<TokenBreakdown> EstimateTokenUsageAsync(string input, string systemPrompt, List<string> history);
        Task<bool> IsWithinTokenLimitAsync(string content, int maxTokens);
    }
}
