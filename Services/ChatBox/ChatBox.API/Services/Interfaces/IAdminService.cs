using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces
{
    public interface IAdminService
    {
        #region AI Configuration Management

        Task<List<AIConfigurationResponse>> GetAIConfigurationsAsync();
        Task<AIConfigurationResponse> CreateAIConfigurationAsync(AIConfigurationRequest request, string userId);
        Task<AIConfigurationResponse> UpdateAIConfigurationAsync(string id, AIConfigurationRequest request, string userId);
        Task<bool> DeleteAIConfigurationAsync(string id);

        // Model management
        Task<bool> SetActiveModelAsync(string modelName, string userId);
        Task<ModelTestResponse> TestModelAsync(string modelName, string userId);
        #endregion

        #region System Statistics
        Task<SystemStatisticsResponse> GetSystemStatisticsAsync();
        Task<List<DailyActivityResponse>> GetDailyActivityAsync(int days = 30);
        Task<List<ModelUsageStatistics>> GetModelUsageStatisticsAsync();
        #endregion
    }
}
