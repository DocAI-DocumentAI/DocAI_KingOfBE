using System.Text.Json;
using AutoMapper;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class PreferenceService : IPreferenceService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IMapper _mapper;

        public PreferenceService(IUnitOfWork<ChatBoxDbContext> unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<UserPreferenceResponse> GetUserChatPreferencesAsync(string userId)
        {
            var userPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null && p.ApplyToNewChats == true);

            if (userPreference == null)
            {
                return new UserPreferenceResponse { UserId = userId };
            }

            return new UserPreferenceResponse
            {
                UserId = userId,
                UserName = userPreference.UserName ?? "",
                ChatbotCharacteristics = ParseChatbotCharacteristics(userPreference.ChatbotCharacteristics),
                AdditionalInfo = userPreference.AdditionalInfo ?? "",
                ApplyToNewChats = userPreference.ApplyToNewChats,
                HasAnyPreferences = !string.IsNullOrEmpty(userPreference.UserName) ||
                                  !string.IsNullOrEmpty(userPreference.ChatbotCharacteristics) ||
                                  !string.IsNullOrEmpty(userPreference.AdditionalInfo)
            };
        }
        public async Task<UserPreferenceResponse> UpdateUserChatPreferencesAsync(string userId, UpdatePreferenceRequest request)
        {
            var existingPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null && p.ApplyToNewChats == true);

            if (existingPreference != null)
            {
                // ✅ Update existing user-level preference
                if (!string.IsNullOrEmpty(request.UserName))
                    existingPreference.UserName = request.UserName;

                if (request.ChatbotCharacteristics?.Any() == true)
                    existingPreference.ChatbotCharacteristics = JsonSerializer.Serialize(request.ChatbotCharacteristics);

                if (!string.IsNullOrEmpty(request.AdditionalInfo))
                    existingPreference.AdditionalInfo = request.AdditionalInfo;

                existingPreference.ApplyToNewChats = request.ApplyToNewChats;
                existingPreference.UpdatedAt = DateTime.UtcNow;
                existingPreference.UpdatedBy = userId;

                _unitOfWork.GetRepository<UserPreference>().UpdateAsync(existingPreference);
            }
            else
            {
                // ✅ Create new user-level preference
                var newPreference = new UserPreference
                {
                    UserId = userId,
                    SessionId = null, // User-level
                    UserName = request.UserName,
                    ChatbotCharacteristics = request.ChatbotCharacteristics?.Any() == true
                        ? JsonSerializer.Serialize(request.ChatbotCharacteristics)
                        : null,
                    AdditionalInfo = request.AdditionalInfo,
                    ApplyToNewChats = request.ApplyToNewChats,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    UpdatedBy = userId
                };

                await _unitOfWork.GetRepository<UserPreference>().InsertAsync(newPreference);
            }

            await _unitOfWork.CommitAsync();
            return await GetUserChatPreferencesAsync(userId);
        }

        public async Task<UserPreferenceResponse> GetSessionPreferencesAsync(string sessionId, string userId)
        {
            var sessionPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == sessionId);

            if (sessionPreference == null)
            {
                return new UserPreferenceResponse { UserId = userId, SessionId = sessionId };
            }

            return new UserPreferenceResponse
            {
                UserId = userId,
                SessionId = sessionId,
                UserName = sessionPreference.UserName ?? "",
                ChatbotCharacteristics = ParseChatbotCharacteristics(sessionPreference.ChatbotCharacteristics),
                AdditionalInfo = sessionPreference.AdditionalInfo ?? "",
                ApplyToNewChats = sessionPreference.ApplyToNewChats,
                HasAnyPreferences = !string.IsNullOrEmpty(sessionPreference.UserName) ||
                                  !string.IsNullOrEmpty(sessionPreference.ChatbotCharacteristics) ||
                                  !string.IsNullOrEmpty(sessionPreference.AdditionalInfo)
            };
        }

        public async Task<UserPreferenceResponse> UpdateSessionPreferencesAsync(string sessionId, string userId, UpdatePreferenceRequest request)
        {
            var existingPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == sessionId);

            if (existingPreference != null)
            {
                if (!string.IsNullOrEmpty(request.UserName))
                    existingPreference.UserName = request.UserName;

                if (request.ChatbotCharacteristics?.Any() == true)
                    existingPreference.ChatbotCharacteristics = JsonSerializer.Serialize(request.ChatbotCharacteristics);

                if (!string.IsNullOrEmpty(request.AdditionalInfo))
                    existingPreference.AdditionalInfo = request.AdditionalInfo;

                existingPreference.ApplyToNewChats = request.ApplyToNewChats;
                existingPreference.UpdatedAt = DateTime.UtcNow;
                existingPreference.UpdatedBy = userId;

                _unitOfWork.GetRepository<UserPreference>().UpdateAsync(existingPreference);
            }
            else
            {
                // ✅ Create new session preference
                var newPreference = new UserPreference
                {
                    UserId = userId,
                    SessionId = sessionId, // Session-specific
                    UserName = request.UserName,
                    ChatbotCharacteristics = request.ChatbotCharacteristics?.Any() == true
                        ? JsonSerializer.Serialize(request.ChatbotCharacteristics)
                        : null,
                    AdditionalInfo = request.AdditionalInfo,
                    ApplyToNewChats = request.ApplyToNewChats,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    UpdatedBy = userId
                };

                await _unitOfWork.GetRepository<UserPreference>().InsertAsync(newPreference);
            }

            await _unitOfWork.CommitAsync();
            return await GetSessionPreferencesAsync(sessionId, userId);
        }
        public async Task<UserPreferenceResponse> GetEffectivePreferencesAsync(string sessionId, string userId)
        {
            // Priority 1: Session-specific preferences
            var sessionPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == sessionId);

            // Priority 2: User-level preferences with ApplyToNewChats = true
            var userPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null && p.ApplyToNewChats == true);

            // ✅ Merge preferences với session override user
            var effectivePreference = new UserPreferenceResponse
            {
                UserId = userId,
                SessionId = sessionId,
                UserName = sessionPreference?.UserName ?? userPreference?.UserName ?? "",
                // ✅ LIMIT characteristics để tránh system prompt quá dài
                ChatbotCharacteristics = LimitCharacteristics(
             sessionPreference?.ChatbotCharacteristics != null
                 ? ParseChatbotCharacteristics(sessionPreference.ChatbotCharacteristics)
                 : ParseChatbotCharacteristics(userPreference?.ChatbotCharacteristics)),
                AdditionalInfo = LimitAdditionalInfo(sessionPreference?.AdditionalInfo ?? userPreference?.AdditionalInfo ?? ""),
                ApplyToNewChats = sessionPreference?.ApplyToNewChats ?? userPreference?.ApplyToNewChats ?? false
            };

            effectivePreference.HasAnyPreferences =
                !string.IsNullOrEmpty(effectivePreference.UserName) ||
                effectivePreference.ChatbotCharacteristics.Any() ||
                !string.IsNullOrEmpty(effectivePreference.AdditionalInfo);

            return effectivePreference;
        }
        public async Task<bool> DeleteUserPreferencesAsync(string userId)
        {
            var preference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null && p.ApplyToNewChats == true);

            if (preference == null)
                return false;

            _unitOfWork.GetRepository<UserPreference>().DeleteAsync(preference);
            await _unitOfWork.CommitAsync();
            return true;
        }

        public async Task<bool> DeleteSessionPreferencesAsync(string sessionId, string userId)
        {
            var preference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == sessionId);

            if (preference == null)
                return false;

            _unitOfWork.GetRepository<UserPreference>().DeleteAsync(preference);
            await _unitOfWork.CommitAsync();
            return true;
        }


        public async Task<bool> HasUserPreferencesAsync(string userId)
        {
            var preference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null && p.ApplyToNewChats == true);

            return preference != null && (
                !string.IsNullOrEmpty(preference.UserName) ||
                !string.IsNullOrEmpty(preference.ChatbotCharacteristics) ||
                !string.IsNullOrEmpty(preference.AdditionalInfo)
            );
        }

        // ✅ HELPER METHODS

        private List<string> ParseChatbotCharacteristics(string? characteristicsJson)
        {
            if (string.IsNullOrEmpty(characteristicsJson))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(characteristicsJson) ?? new List<string>();
            }
            catch
            {
                return characteristicsJson.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();
            }
        }

        public async Task<List<PreferenceResponse>> GetSessionPreferencesAsync(string sessionId)
        {
            var session = await _unitOfWork.GetRepository<ChatSession>()
                .SingleOrDefaultAsync(predicate: s => s.Id == sessionId);

            if (session == null) return new List<PreferenceResponse>();

            var preferences = await GetEffectivePreferencesAsync(sessionId, session.UserId);

            var result = new List<PreferenceResponse>();

            if (!string.IsNullOrEmpty(preferences.UserName))
                result.Add(new PreferenceResponse { Key = "UserName", Value = preferences.UserName });

            if (preferences.ChatbotCharacteristics.Any())
                result.Add(new PreferenceResponse { Key = "ChatbotCharacter", Value = JsonSerializer.Serialize(preferences.ChatbotCharacteristics) });

            if (!string.IsNullOrEmpty(preferences.AdditionalInfo))
                result.Add(new PreferenceResponse { Key = "AdditionalInfo", Value = preferences.AdditionalInfo });

            return result;
        }
        private List<string> LimitCharacteristics(List<string> characteristics)
        {
            // Chỉ lấy 2 characteristics đầu tiên để tránh system prompt quá dài
            return characteristics.Take(2).ToList();
        }

        private string LimitAdditionalInfo(string additionalInfo)
        {
            // Giới hạn additional info tối đa 100 ký tự
            if (string.IsNullOrEmpty(additionalInfo))
                return "";

            return additionalInfo.Length > 100
                ? additionalInfo.Substring(0, 100) + "..."
                : additionalInfo;
        }
    }

}

