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
    public class PreferenceService : IPreferenceService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IMapper _mapper;

        public PreferenceService(IUnitOfWork<ChatBoxDbContext> unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        /// <summary>
        /// ✅ MAIN METHOD: Lấy preference hiệu quả cho session (Simple logic: Session Override > User Default > Empty)
        /// </summary>
        public async Task<UserPreferenceResponse> GetEffectivePreferencesAsync(string sessionId, string userId)
        {
            // Step 1: Lấy Session Override (SessionId = specific value)
            var sessionPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == sessionId);

            // Step 2: Lấy User Default (SessionId = NULL)
            var userPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null);

            // Step 3: Merge theo priority: Session Override > User Default > Empty
            var effectiveCharacteristics = LimitCharacteristics(
                sessionPreference?.ChatbotCharacteristics != null
                    ? ParseChatbotCharacteristics(sessionPreference.ChatbotCharacteristics)
                    : ParseChatbotCharacteristics(userPreference?.ChatbotCharacteristics));

            var effectivePreference = new UserPreferenceResponse
            {
                UserId = userId,
                SessionId = sessionId,
                UserName = sessionPreference?.UserName ?? userPreference?.UserName ?? "",
                ChatbotCharacteristics = effectiveCharacteristics,
                AdditionalInfo = LimitAdditionalInfo(sessionPreference?.AdditionalInfo ?? userPreference?.AdditionalInfo ?? ""),
                AvailableCharacteristics = BuildAvailableCharacteristics(effectiveCharacteristics)
            };

            effectivePreference.HasAnyPreferences =
                !string.IsNullOrEmpty(effectivePreference.UserName) ||
                effectivePreference.ChatbotCharacteristics.Any() ||
                !string.IsNullOrEmpty(effectivePreference.AdditionalInfo);

            return effectivePreference;
        }

        /// <summary>
        /// Lấy tùy chọn cá nhân của user (User Default - SessionId = NULL)
        /// </summary>
        public async Task<UserPreferenceResponse> GetUserChatPreferencesAsync(string userId)
        {
            var userPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null);

            if (userPreference == null)
            {
                return new UserPreferenceResponse
                {
                    UserId = userId,
                    AvailableCharacteristics = BuildAvailableCharacteristics(new List<string>())
                };
            }

            var selectedCharacteristics = ParseChatbotCharacteristics(userPreference.ChatbotCharacteristics);

            return new UserPreferenceResponse
            {
                UserId = userId,
                UserName = userPreference.UserName ?? "",
                ChatbotCharacteristics = selectedCharacteristics,
                AdditionalInfo = userPreference.AdditionalInfo ?? "",
                HasAnyPreferences = !string.IsNullOrEmpty(userPreference.UserName) ||
                                  !string.IsNullOrEmpty(userPreference.ChatbotCharacteristics) ||
                                  !string.IsNullOrEmpty(userPreference.AdditionalInfo),
                AvailableCharacteristics = BuildAvailableCharacteristics(selectedCharacteristics)
            };
        }

        /// <summary>
        /// Cập nhật tùy chọn cá nhân (User Default - SessionId = NULL)
        /// </summary>
        public async Task<UserPreferenceResponse> UpdateUserChatPreferencesAsync(string userId, UpdatePreferenceRequest request)
        {
            if (request.ChatbotCharacteristics != null && request.ChatbotCharacteristics.Any())
            {
                var invalidCharacteristics = request.ChatbotCharacteristics
                    .Where(c => !ChatbotCharacteristics.IsValidCharacteristic(c))
                    .ToList();

                if (invalidCharacteristics.Any())
                {
                    throw new ArgumentException($"Đặc điểm không hợp lệ: {string.Join(", ", invalidCharacteristics)}");
                }

                request.ChatbotCharacteristics = request.ChatbotCharacteristics
                    .Where(ChatbotCharacteristics.IsValidCharacteristic)
                    .Take(ChatConstants.MaxCharacteristics)
                    .ToList();
            }

            if (!string.IsNullOrEmpty(request.AdditionalInfo) && request.AdditionalInfo.Length > ChatConstants.MaxAdditionalInfoLength)
            {
                request.AdditionalInfo = request.AdditionalInfo.Substring(0, ChatConstants.MaxAdditionalInfoLength);
            }

            var existingPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null);

            if (existingPreference != null)
            {
                // ✅ SAME: UserName logic OK
                if (request.UserName != null)
                    existingPreference.UserName = string.IsNullOrEmpty(request.UserName) ? null : request.UserName;

                // ✅ FIXED: ChatbotCharacteristics logic
                if (request.ChatbotCharacteristics != null)
                {
                    // Nếu array empty → set null để clear
                    // Nếu array có items → serialize
                    existingPreference.ChatbotCharacteristics = request.ChatbotCharacteristics.Any()
                        ? JsonSerializer.Serialize(request.ChatbotCharacteristics)
                        : null; // ✅ FIX: null thay vì "[]"
                }

                // ✅ SAME: AdditionalInfo logic OK  
                if (request.AdditionalInfo != null)
                    existingPreference.AdditionalInfo = string.IsNullOrEmpty(request.AdditionalInfo) ? null : request.AdditionalInfo;

                existingPreference.UpdatedAt = DateTime.UtcNow;
                existingPreference.UpdatedBy = userId;

                _unitOfWork.GetRepository<UserPreference>().UpdateAsync(existingPreference);
            }
            else
            {
                // ✅ IMPROVED: Create logic - only create if has meaningful data
                if (HasMeaningfulData(request))
                {
                    var newPreference = new UserPreference
                    {
                        UserId = userId,
                        SessionId = null,
                        UserName = string.IsNullOrEmpty(request.UserName) ? null : request.UserName,
                        ChatbotCharacteristics = request.ChatbotCharacteristics?.Any() == true
                            ? JsonSerializer.Serialize(request.ChatbotCharacteristics)
                            : null, // ✅ FIX: null thay vì "[]"
                        AdditionalInfo = string.IsNullOrEmpty(request.AdditionalInfo) ? null : request.AdditionalInfo,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = userId,
                        UpdatedBy = userId
                    };

                    await _unitOfWork.GetRepository<UserPreference>().InsertAsync(newPreference);
                }
            }

            await _unitOfWork.CommitAsync();
            return await GetUserChatPreferencesAsync(userId);
        }
        private bool HasMeaningfulData(UpdatePreferenceRequest request)
        {
            return !string.IsNullOrEmpty(request.UserName) ||
                   (request.ChatbotCharacteristics?.Any() == true) ||
                   !string.IsNullOrEmpty(request.AdditionalInfo);
        }
        /// <summary>
        /// Lấy tùy chọn cho session (Effective Preferences)
        /// </summary>
        public async Task<UserPreferenceResponse> GetSessionPreferencesAsync(string sessionId, string userId)
        {
            return await GetEffectivePreferencesAsync(sessionId, userId);
        }

        /// <summary>
        /// Cập nhật tùy chọn cho session cụ thể (Session Override - SessionId = specific value)
        /// </summary>
        public async Task<UserPreferenceResponse> UpdateSessionPreferencesAsync(string sessionId, string userId, UpdatePreferenceRequest request)
        {
            if (request.ChatbotCharacteristics != null && request.ChatbotCharacteristics.Any())
            {
                var invalidCharacteristics = request.ChatbotCharacteristics
                    .Where(c => !ChatbotCharacteristics.IsValidCharacteristic(c))
                    .ToList();

                if (invalidCharacteristics.Any())
                {
                    throw new ArgumentException($"Đặc điểm không hợp lệ: {string.Join(", ", invalidCharacteristics)}");
                }

                request.ChatbotCharacteristics = request.ChatbotCharacteristics
                    .Where(ChatbotCharacteristics.IsValidCharacteristic)
                    .Take(ChatConstants.MaxCharacteristics)
                    .ToList();
            }

            if (!string.IsNullOrEmpty(request.AdditionalInfo) && request.AdditionalInfo.Length > ChatConstants.MaxAdditionalInfoLength)
            {
                request.AdditionalInfo = request.AdditionalInfo.Substring(0, ChatConstants.MaxAdditionalInfoLength);
            }

            var existingPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == sessionId);

            if (existingPreference != null)
            {
                // ✅ SAME: Update existing session override
                if (request.UserName != null)
                    existingPreference.UserName = string.IsNullOrEmpty(request.UserName) ? null : request.UserName;

                // ✅ FIXED: Same fix for ChatbotCharacteristics
                if (request.ChatbotCharacteristics != null)
                {
                    existingPreference.ChatbotCharacteristics = request.ChatbotCharacteristics.Any()
                        ? JsonSerializer.Serialize(request.ChatbotCharacteristics)
                        : null; // ✅ FIX: null thay vì "[]"
                }

                if (request.AdditionalInfo != null)
                    existingPreference.AdditionalInfo = string.IsNullOrEmpty(request.AdditionalInfo) ? null : request.AdditionalInfo;

                existingPreference.UpdatedAt = DateTime.UtcNow;
                existingPreference.UpdatedBy = userId;

                _unitOfWork.GetRepository<UserPreference>().UpdateAsync(existingPreference);
            }
            else
            {
                // ✅ IMPROVED: Only create if has meaningful data
                if (HasMeaningfulData(request))
                {
                    var newPreference = new UserPreference
                    {
                        UserId = userId,
                        SessionId = sessionId,
                        UserName = string.IsNullOrEmpty(request.UserName) ? null : request.UserName,
                        ChatbotCharacteristics = request.ChatbotCharacteristics?.Any() == true
                            ? JsonSerializer.Serialize(request.ChatbotCharacteristics)
                            : null, // ✅ FIX: null thay vì "[]"
                        AdditionalInfo = string.IsNullOrEmpty(request.AdditionalInfo) ? null : request.AdditionalInfo,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = userId,
                        UpdatedBy = userId
                    };

                    await _unitOfWork.GetRepository<UserPreference>().InsertAsync(newPreference);
                }
            }

            await _unitOfWork.CommitAsync();
            return await GetEffectivePreferencesAsync(sessionId, userId);
        }

        /// <summary>
        /// Xóa tùy chọn cá nhân của user (User Default)
        /// </summary>
        public async Task<bool> DeleteUserPreferencesAsync(string userId)
        {
            var preference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null);

            if (preference == null)
                return false;

            _unitOfWork.GetRepository<UserPreference>().DeleteAsync(preference);
            await _unitOfWork.CommitAsync();
            return true;
        }

        /// <summary>
        /// Xóa tùy chọn riêng của session (Session Override)
        /// </summary>
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
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null);

            return preference != null && (
                !string.IsNullOrEmpty(preference.UserName) ||
                !string.IsNullOrEmpty(preference.ChatbotCharacteristics) ||
                !string.IsNullOrEmpty(preference.AdditionalInfo)
            );
        }

        // ✅ HELPER METHODS - giữ nguyên style cũ

        private List<CharacteristicOption> BuildAvailableCharacteristics(List<string> selectedCharacteristics)
        {
            return ChatbotCharacteristics.Available.Select(c => new CharacteristicOption
            {
                Value = c.Value,
                DisplayName = c.DisplayName,
                IsSelected = selectedCharacteristics.Contains(c.Value)
            }).ToList();
        }

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
            return characteristics.Take(ChatConstants.MaxCharacteristics).ToList();
        }

        private string LimitAdditionalInfo(string additionalInfo)
        {
            if (string.IsNullOrEmpty(additionalInfo))
                return "";

            return additionalInfo.Length > ChatConstants.MaxAdditionalInfoLength
                ? additionalInfo.Substring(0, ChatConstants.MaxAdditionalInfoLength) + "..."
                : additionalInfo;
        }

        /// <summary>
        /// ✅ NEW: Get preference status for UI indicators
        /// </summary>
        public async Task<PreferenceStatusResponse> GetPreferenceStatusAsync(string sessionId, string userId)
        {
            // Get session override
            var sessionPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == sessionId);

            // Get user default
            var userPreference = await _unitOfWork.GetRepository<UserPreference>()
                .SingleOrDefaultAsync(predicate: p => p.UserId == userId && p.SessionId == null);

            var status = new PreferenceStatusResponse();

            if (sessionPreference != null)
            {
                // Has session override
                status.CurrentSource = "SessionOverride";
                status.DisplayName = sessionPreference.UserName ?? "User";
                status.StatusBadge = "🔵 Tùy chọn riêng";
                status.StatusColor = "blue";
                status.HasOverride = true;

                var sessionChars = ParseChatbotCharacteristics(sessionPreference.ChatbotCharacteristics);
                status.CurrentCharacteristics = sessionChars.Select(c => ChatbotCharacteristics.GetDisplayName(c)).ToList();
                status.CurrentAdditionalInfo = sessionPreference.AdditionalInfo ?? "";
            }
            else if (userPreference != null)
            {
                // Using user default
                status.CurrentSource = "UserDefault";
                status.DisplayName = userPreference.UserName ?? "User";
                status.StatusBadge = "🟢 Mặc định";
                status.StatusColor = "green";
                status.HasOverride = false;

                var userChars = ParseChatbotCharacteristics(userPreference.ChatbotCharacteristics);
                status.CurrentCharacteristics = userChars.Select(c => ChatbotCharacteristics.GetDisplayName(c)).ToList();
                status.CurrentAdditionalInfo = userPreference.AdditionalInfo ?? "";
            }
            else
            {
                // No preferences
                status.CurrentSource = "None";
                status.DisplayName = "User";
                status.StatusBadge = "⚪ Chưa thiết lập";
                status.StatusColor = "gray";
                status.HasOverride = false;
                status.CurrentCharacteristics = new List<string>();
                status.CurrentAdditionalInfo = "";
            }

            // Always include user default for fallback info
            if (userPreference != null)
            {
                var fallbackChars = ParseChatbotCharacteristics(userPreference.ChatbotCharacteristics);
                status.FallbackUserDefault = new UserDefaultInfo
                {
                    UserName = userPreference.UserName ?? "",
                    Characteristics = fallbackChars.Select(c => ChatbotCharacteristics.GetDisplayName(c)).ToList(),
                    AdditionalInfo = userPreference.AdditionalInfo ?? ""
                };
            }

            return status;
        }
    }
}

