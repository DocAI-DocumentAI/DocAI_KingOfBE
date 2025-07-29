using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces
{
    public interface IPreferenceService
    {
        Task<List<PreferenceResponse>> GetSessionPreferencesAsync(string sessionId);
        Task<PreferenceResponse> UpdatePreferenceAsync(string sessionId, UpdatePreferenceRequest request);
        Task<bool> DeletePreferenceAsync(string sessionId, string key);
        Task<string> GetPreferenceValueAsync(string sessionId, string key);
    }
}
