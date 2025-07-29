using System.Text.Json;
using AutoMapper;
using ChatBox.API.Constants;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class UserPreferenceService : IUserPreferenceService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IMapper _mapper;

        public UserPreferenceService(IUnitOfWork<ChatBoxDbContext> unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<UserPreferenceResponse> GetUserPreferencesAsync(string sessionId)
        {
            var preferences = await _unitOfWork.GetRepository<SessionPreference>()
                .GetListAsync(predicate: p => p.SessionId == sessionId);

            var response = new UserPreferenceResponse();

            var userNamePref = preferences.FirstOrDefault( predicate: p => p.Key == PreferenceKeys.UserName);
            if (userNamePref != null)
                response.UserName = userNamePref.Value;

            var characteristicsPref = preferences.FirstOrDefault(predicate:  p => p.Key == PreferenceKeys.ChatbotCharacter);
            if (characteristicsPref != null && !string.IsNullOrEmpty(characteristicsPref.Value))
            {
                try
                {
                    response.ChatbotCharacteristics = JsonSerializer.Deserialize<List<string>>(characteristicsPref.Value) ?? new();
                }
                catch
                {
                    // Fallback for old single value format
                    response.ChatbotCharacteristics = new List<string> { characteristicsPref.Value };
                }
            }

            var additionalInfoPref = preferences.FirstOrDefault(predicate: p => p.Key == PreferenceKeys.AdditionalInfo);
            if (additionalInfoPref != null)
                response.AdditionalInfo = additionalInfoPref.Value;

            response.AvailableCharacteristics = await GetAvailableCharacteristicsAsync();

            return response;
        }

        public async Task<UserPreferenceResponse> UpdateUserPreferencesAsync(string sessionId, UserPreferenceRequest request)
        {
            // Validate characteristics
            if (request.ChatbotCharacteristics?.Any() == true)
            {
                var invalidCharacteristics = request.ChatbotCharacteristics
                    .Where(c => !ChatbotCharacteristics.IsValidCharacteristic(c))
                    .ToList();

                if (invalidCharacteristics.Any())
                {
                    throw new ArgumentException($"Đặc điểm không hợp lệ: {string.Join(", ", invalidCharacteristics)}");
                }
            }

            // Update user name
            if (!string.IsNullOrEmpty(request.UserName))
            {
                await UpdatePreferenceAsync(sessionId, PreferenceKeys.UserName, request.UserName);
            }

            // Update characteristics
            if (request.ChatbotCharacteristics?.Any() == true)
            {
                var characteristicsJson = JsonSerializer.Serialize(request.ChatbotCharacteristics);
                await UpdatePreferenceAsync(sessionId, PreferenceKeys.ChatbotCharacter, characteristicsJson);
            }

            // Update additional info
            if (!string.IsNullOrEmpty(request.AdditionalInfo))
            {
                await UpdatePreferenceAsync(sessionId, PreferenceKeys.AdditionalInfo, request.AdditionalInfo);
            }

            await _unitOfWork.CommitAsync();

            return await GetUserPreferencesAsync(sessionId);
        }

        public async Task<List<CharacteristicOption>> GetAvailableCharacteristicsAsync()
        {
            return await Task.FromResult(ChatbotCharacteristics.Available
                .Select(c => new CharacteristicOption
                {
                    Value = c.Value,
                    DisplayName = c.DisplayName
                })
                .ToList());
        }

        private async Task UpdatePreferenceAsync(string sessionId, string key, string value)
        {
            var existingPreference = await _unitOfWork.GetRepository<SessionPreference>()
                .SingleOrDefaultAsync(predicate: p => p.SessionId == sessionId && p.Key == key);

            if (existingPreference != null)
            {
                existingPreference.Value = value;
                existingPreference.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.GetRepository<SessionPreference>().UpdateAsync(existingPreference);
            }
            else
            {
                var newPreference = new SessionPreference
                {
                    SessionId = sessionId,
                    Key = key,
                    Value = value
                };
                _unitOfWork.GetRepository<SessionPreference>().UpdateAsync(newPreference);
            }
        }
    }
}
