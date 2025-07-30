using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces
{
    public interface IPreferenceService
    {
        Task<UserPreferenceResponse> GetUserChatPreferencesAsync(string userId);
        Task<UserPreferenceResponse> UpdateUserChatPreferencesAsync(string userId, UpdatePreferenceRequest request);
        Task<bool> DeleteUserPreferencesAsync(string userId);
        Task<bool> HasUserPreferencesAsync(string userId);

        Task<UserPreferenceResponse> GetSessionPreferencesAsync(string sessionId, string userId);
        Task<UserPreferenceResponse> UpdateSessionPreferencesAsync(string sessionId, string userId, UpdatePreferenceRequest request);
        Task<bool> DeleteSessionPreferencesAsync(string sessionId, string userId);

        Task<UserPreferenceResponse> GetEffectivePreferencesAsync(string sessionId, string userId);

        Task<List<PreferenceResponse>> GetSessionPreferencesAsync(string sessionId);
    }
}
