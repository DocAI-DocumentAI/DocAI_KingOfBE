using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Request.UserPreferenceService;
using ChatBox.API.Payload.Response.UserPreferenceResponse;
using ChatBox.Domain.Models;

namespace ChatBox.API.Services.Interfaces
{
    public interface IUserPreferenceService
    {
        // Core operations
        Task<UserPreference> GetPreferenceAsync(Guid userId);
        Task<UserPreferenceResponse> GetPreferenceResponseAsync(Guid userId);
        Task<bool> UpdatePreferenceAsync(Guid userId, UpdatePreferencesRequest request);
        Task<bool> ResetPreferencesAsync(Guid userId);

        Task<UserPreferenceResponse> GetDefaultPreferencesAsync();
        Task<bool> SetDefaultPreferencesAsync(SetDefaultPreferencesRequest request);
    }
}