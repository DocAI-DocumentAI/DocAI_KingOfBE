using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces
{
    public interface IAdminService
    {
        Task<List<AIConfigurationResponse>> GetAIConfigurationsAsync();
        Task<AIConfigurationResponse> CreateAIConfigurationAsync(AIConfigurationRequest request, string userId);
        Task<AIConfigurationResponse> UpdateAIConfigurationAsync(string id, AIConfigurationRequest request, string userId);
        Task<bool> DeleteAIConfigurationAsync(string id);
    }
}
