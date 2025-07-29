using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces
{
    public interface IUserPreferenceService
    {
        Task<UserPreferenceResponse> GetUserPreferencesAsync(string sessionId);
        Task<UserPreferenceResponse> UpdateUserPreferencesAsync(string sessionId, UserPreferenceRequest request);
        Task<List<CharacteristicOption>> GetAvailableCharacteristicsAsync();
    }
}
