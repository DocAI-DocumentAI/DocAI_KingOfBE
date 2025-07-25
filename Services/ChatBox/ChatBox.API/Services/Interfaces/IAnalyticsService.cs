using ChatBox.API.Payload.Response.AnalyticsResponse;

namespace ChatBox.API.Services.Interfaces
{
    public interface IAnalyticsService
    {
        Task<ConversationAnalytics> GetUserAnalyticsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<UsageStatsResponse> GetUsageStatsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null);
    }
}
