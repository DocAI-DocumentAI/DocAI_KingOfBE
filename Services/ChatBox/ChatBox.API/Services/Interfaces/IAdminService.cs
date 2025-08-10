using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces
{
    public interface IAdminService
    {

        // ✅ EXISTING methods (keep unchanged)
        Task<List<AIConfigurationResponse>> GetAIConfigurationsAsync();
        Task<AIConfigurationResponse> CreateAIConfigurationAsync(AIConfigurationRequest request, string userId);
        Task<AIConfigurationResponse> UpdateAIConfigurationAsync(string id, AIConfigurationRequest request, string userId);
        Task<bool> DeleteAIConfigurationAsync(string id);
        Task<ModelTestResponse> TestModelAsync(string modelName, string userId);
        Task<SystemStatisticsResponse> GetSystemStatisticsAsync();
        Task<List<DailyActivityResponse>> GetDailyActivityAsync(int days = 30);
        Task<List<ModelUsageStatistics>> GetModelUsageStatisticsAsync();
        Task<ModelImpactResponse> GetModelImpactAnalysisAsync(string modelName);
        Task<bool> SetMultipleActiveModelsAsync(List<string> modelNames, string userId);

        Task<ModelActivationResponse> TestAndActivateModelAsync(string modelName, string userId);
        Task<bool> DeactivateModelAsync(string modelName, string userId);
        Task<bool> SetDefaultModelAsync(string modelName, string userId); 
    }
}
