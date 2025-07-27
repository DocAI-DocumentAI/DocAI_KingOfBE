using ChatBox.API.Payload.Response.AIServiceResponse;

namespace ChatBox.API.Services.Interfaces
{
    public interface ITokenValidationService
    {
        Task<TokenBreakdown> EstimateTokenUsageAsync(string input, string systemPrompt, List<string> history);
        Task<bool> IsWithinTokenLimitAsync(string content, int maxTokens);
        Task<OptimizedContent> OptimizeContentForTokenLimitAsync(string content, int maxTokens, string optimizationStrategy = "intelligent");
        Task<List<TokenUsageStats>> GetTokenUsageStatsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null);
    }
}
