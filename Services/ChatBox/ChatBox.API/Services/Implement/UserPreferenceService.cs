using System.Text.Json;
using AutoMapper;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Request.UserPreferenceService;
using ChatBox.API.Payload.Response.UserPreferenceResponse;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ChatBox.API.Services.Implement
{
    public class UserPreferenceService : IUserPreferenceService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IAuditService _auditService;
        private readonly ILogger<UserPreferenceService> _logger;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IMapper _mapper;

        private static readonly Dictionary<string, object> DefaultPreferences = new()
        {
            { "Language", "en" },
            { "ResponseStyle", "balanced" },
            { "Tone", "professional" },
            { "MaxResponseLength", 500 },
            { "IncludeCitations", true },
            { "EnableSuggestions", true },
            { "EnableNotifications", true },
            { "TimeZone", "UTC" },
            { "DateFormat", "MM/dd/yyyy" },
            { "Theme", "light" }
        };

        public UserPreferenceService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IAuditService auditService,
            ILogger<UserPreferenceService> logger,
            IConfiguration configuration,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _auditService = auditService;
            _logger = logger;
            _configuration = configuration;
            _mapper = mapper;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<UserPreference> GetPreferenceAsync(Guid userId)
        {
            try
            {
                _logger.LogDebug("Getting preferences for user {UserId}", userId);

                var preferenceRepo = _unitOfWork.GetRepository<UserPreference>();
                var preference = await preferenceRepo.SingleOrDefaultAsync(predicate:
                    p => p.UserId == userId && !p.IsDeleted);

                if (preference == null)
                {
                    _logger.LogInformation("No preferences found for user {UserId}, creating default preferences", userId);
                    preference = await CreateDefaultPreferenceAsync(userId);
                }

                return preference;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting preferences for user {UserId}", userId);
                return await CreateFallbackPreferenceAsync(userId);
            }
        }

        public async Task<UserPreferenceResponse> GetPreferenceResponseAsync(Guid userId)
        {
            try
            {
                var preference = await GetPreferenceAsync(userId);
                var validationInfo = await ValidatePreferencesAsync(preference);

                // Map using AutoMapper and add additional properties
                var response = _mapper.Map<UserPreferenceResponse>(preference);
                response.CustomSettings = ParseJsonToDictionary(preference.CustomSettings);
                response.PreferredTopics = ParseJsonToList(preference.PreferredTopics);
                response.BlockedTopics = ParseJsonToList(preference.BlockedTopics);
                response.IsDefault = IsDefaultPreference(preference);
                response.ValidationInfo = validationInfo;

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting preference response for user {UserId}", userId);
                return await CreateFallbackPreferenceResponseAsync(userId);
            }
        }

        public async Task<bool> UpdatePreferenceAsync(Guid userId, UpdatePreferencesRequest request)
        {
            try
            {
                _logger.LogInformation("Updating preferences for user {UserId}", userId);

                var preferenceRepo = _unitOfWork.GetRepository<UserPreference>();
                var preference = await preferenceRepo.SingleOrDefaultAsync(predicate:
                    p => p.UserId == userId && !p.IsDeleted);

                var oldPreference = preference != null ? ClonePreference(preference) : null;

                if (preference == null)
                {
                    // Create new preference using AutoMapper
                    preference = _mapper.Map<UserPreference>(request);
                    preference.Id = Guid.NewGuid();
                    preference.UserId = userId;
                    preference.CreatedAt = DateTime.UtcNow;
                }

                // Validate request
                var validationResult = await ValidateUpdateRequestAsync(request);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Invalid preference update request for user {UserId}: {Errors}",
                        userId, string.Join(", ", validationResult.ValidationErrors));
                    return false;
                }

                // Update fields
                await UpdatePreferenceFieldsAsync(preference, request);
                preference.UpdatedAt = DateTime.UtcNow;

                if (oldPreference == null)
                {
                    await preferenceRepo.InsertAsync(preference);
                }
                else
                {
                    preferenceRepo.UpdateAsync(preference);
                }

                await _unitOfWork.CommitAsync();

                // Log audit trail
                await _auditService.LogAsync(userId, "UpdatePreferences", "UserPreference", preference.Id.ToString(),
                    oldPreference, preference);

                _logger.LogInformation("Preferences updated successfully for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating preferences for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ResetPreferencesAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Resetting preferences for user {UserId}", userId);

                var preferenceRepo = _unitOfWork.GetRepository<UserPreference>();
                var preference = await preferenceRepo.SingleOrDefaultAsync(predicate:
                    p => p.UserId == userId && !p.IsDeleted);

                if (preference == null)
                {
                    _logger.LogInformation("No preferences found to reset for user {UserId}", userId);
                    return true;
                }

                var oldPreference = ClonePreference(preference);

                // Reset to defaults
                await ResetToDefaultValuesAsync(preference);
                preference.UpdatedAt = DateTime.UtcNow;

                preferenceRepo.UpdateAsync(preference);
                await _unitOfWork.CommitAsync();

                // Log audit trail
                await _auditService.LogAsync(userId, "ResetPreferences", "UserPreference", preference.Id.ToString(),
                    oldPreference, preference);

                _logger.LogInformation("Preferences reset successfully for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting preferences for user {UserId}", userId);
                return false;
            }
        }

        public async Task<UserPreferenceResponse> GetDefaultPreferencesAsync()
        {
            try
            {
                var systemPreferences = await GetSystemPreferencesAsync();

                return new UserPreferenceResponse
                {
                    UserId = Guid.Empty,
                    Language = GetDefaultValue<string>("Language", systemPreferences),
                    ResponseStyle = GetDefaultValue<string>("ResponseStyle", systemPreferences),
                    Tone = GetDefaultValue<string>("Tone", systemPreferences),
                    MaxResponseLength = GetDefaultValue<int>("MaxResponseLength", systemPreferences),
                    IncludeCitations = GetDefaultValue<bool>("IncludeCitations", systemPreferences),
                    EnableSuggestions = GetDefaultValue<bool>("EnableSuggestions", systemPreferences),
                    EnableNotifications = GetDefaultValue<bool>("EnableNotifications", systemPreferences),
                    TimeZone = GetDefaultValue<string>("TimeZone", systemPreferences),
                    DateFormat = GetDefaultValue<string>("DateFormat", systemPreferences),
                    Theme = GetDefaultValue<string>("Theme", systemPreferences),
                    CustomSettings = new Dictionary<string, object>(),
                    PreferredTopics = new List<string>(),
                    BlockedTopics = new List<string>(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null,
                    IsDefault = true,
                    ValidationInfo = new PreferenceValidationInfo { IsValid = true }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default preferences");
                return CreateHardcodedDefaultPreference();
            }
        }

        public async Task<bool> SetDefaultPreferencesAsync(SetDefaultPreferencesRequest request)
        {
            try
            {
                _logger.LogInformation("Setting default preference: {SettingName} = {DefaultValue}",
                    request.SettingName, request.DefaultValue);

                var systemPrefRepo = _unitOfWork.GetRepository<SystemPreference>();
                var existingPref = await systemPrefRepo.SingleOrDefaultAsync(predicate:
                    sp => sp.SettingName == request.SettingName && sp.IsActive);

                if (existingPref != null)
                {
                    var oldValue = existingPref.DefaultValue;
                    existingPref.DefaultValue = JsonSerializer.Serialize(request.DefaultValue, _jsonOptions);
                    existingPref.Description = request.Description ?? existingPref.Description;
                    existingPref.Category = request.Category ?? existingPref.Category;
                    existingPref.IsUserConfigurable = request.IsUserConfigurable;
                    existingPref.ValidationRules = JsonSerializer.Serialize(request.ValidationRules, _jsonOptions);
                    existingPref.UpdatedAt = DateTime.UtcNow;

                    systemPrefRepo.UpdateAsync(existingPref);

                    await _auditService.LogAsync(null, "UpdateSystemPreference", "SystemPreference", existingPref.Id.ToString(),
                        new { SettingName = request.SettingName, OldValue = oldValue },
                        new { SettingName = request.SettingName, NewValue = existingPref.DefaultValue });
                }
                else
                {
                    var newPref = new SystemPreference
                    {
                        Id = Guid.NewGuid(),
                        SettingName = request.SettingName,
                        DefaultValue = JsonSerializer.Serialize(request.DefaultValue, _jsonOptions),
                        Description = request.Description,
                        Category = request.Category ?? "General",
                        DataType = request.DefaultValue.GetType().Name,
                        IsUserConfigurable = request.IsUserConfigurable,
                        ValidationRules = JsonSerializer.Serialize(request.ValidationRules, _jsonOptions),
                        AllowedValues = "[]",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    await systemPrefRepo.InsertAsync(newPref);

                    await _auditService.LogAsync(null, "CreateSystemPreference", "SystemPreference", newPref.Id.ToString(),
                        null, new { SettingName = request.SettingName, DefaultValue = request.DefaultValue });
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Default preference set successfully: {SettingName}", request.SettingName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default preference: {SettingName}", request.SettingName);
                return false;
            }
        }

        // Private helper methods
        private async Task<UserPreference> CreateDefaultPreferenceAsync(Guid userId)
        {
            try
            {
                var systemPreferences = await GetSystemPreferencesAsync();

                var preference = new UserPreference
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Language = GetDefaultValue<string>("Language", systemPreferences),
                    ResponseStyle = GetDefaultValue<string>("ResponseStyle", systemPreferences),
                    Tone = GetDefaultValue<string>("Tone", systemPreferences),
                    MaxResponseLength = GetDefaultValue<int>("MaxResponseLength", systemPreferences),
                    IncludeCitations = GetDefaultValue<bool>("IncludeCitations", systemPreferences),
                    EnableSuggestions = GetDefaultValue<bool>("EnableSuggestions", systemPreferences),
                    EnableNotifications = GetDefaultValue<bool>("EnableNotifications", systemPreferences),
                    TimeZone = GetDefaultValue<string>("TimeZone", systemPreferences),
                    DateFormat = GetDefaultValue<string>("DateFormat", systemPreferences),
                    Theme = GetDefaultValue<string>("Theme", systemPreferences),
                    CustomSettings = "{}",
                    PreferredTopics = "[]",
                    BlockedTopics = "[]",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                var preferenceRepo = _unitOfWork.GetRepository<UserPreference>();
                await preferenceRepo.InsertAsync(preference);
                await _unitOfWork.CommitAsync();

                await _auditService.LogAsync(userId, "CreateDefaultPreferences", "UserPreference", preference.Id.ToString(),
                    null, preference);

                return preference;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating default preference for user {UserId}", userId);
                return await CreateFallbackPreferenceAsync(userId);
            }
        }

        private async Task<UserPreference> CreateFallbackPreferenceAsync(Guid userId)
        {
            return new UserPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Language = "en",
                ResponseStyle = "balanced",
                Tone = "professional",
                MaxResponseLength = 500,
                IncludeCitations = true,
                EnableSuggestions = true,
                EnableNotifications = true,
                TimeZone = "UTC",
                DateFormat = "MM/dd/yyyy",
                Theme = "light",
                CustomSettings = "{}",
                PreferredTopics = "[]",
                BlockedTopics = "[]",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
        }

        private async Task<UserPreferenceResponse> CreateFallbackPreferenceResponseAsync(Guid userId)
        {
            var fallbackPref = await CreateFallbackPreferenceAsync(userId);

            return new UserPreferenceResponse
            {
                UserId = fallbackPref.UserId,
                Language = fallbackPref.Language,
                ResponseStyle = fallbackPref.ResponseStyle,
                Tone = fallbackPref.Tone,
                MaxResponseLength = fallbackPref.MaxResponseLength,
                IncludeCitations = fallbackPref.IncludeCitations,
                EnableSuggestions = fallbackPref.EnableSuggestions,
                EnableNotifications = fallbackPref.EnableNotifications,
                TimeZone = fallbackPref.TimeZone,
                DateFormat = fallbackPref.DateFormat,
                Theme = fallbackPref.Theme,
                CustomSettings = new Dictionary<string, object>(),
                PreferredTopics = new List<string>(),
                BlockedTopics = new List<string>(),
                CreatedAt = fallbackPref.CreatedAt,
                UpdatedAt = null,
                IsDefault = true,
                ValidationInfo = new PreferenceValidationInfo { IsValid = true }
            };
        }

        private async Task<List<SystemPreference>> GetSystemPreferencesAsync()
        {
            try
            {
                var systemPrefRepo = await _unitOfWork.GetRepository<SystemPreference>().GetListAsync(predicate: sp => sp.IsActive);
                return systemPrefRepo.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting system preferences, using hardcoded defaults");
                return new List<SystemPreference>();
            }
        }

        private T GetDefaultValue<T>(string settingName, List<SystemPreference> systemPreferences)
        {
            try
            {
                var systemPref = systemPreferences.FirstOrDefault(sp => sp.SettingName == settingName);
                if (systemPref != null)
                {
                    var value = JsonSerializer.Deserialize<T>(systemPref.DefaultValue, _jsonOptions);
                    if (value != null)
                        return value;
                }

                // Fallback to hardcoded default
                if (DefaultPreferences.TryGetValue(settingName, out var defaultValue))
                {
                    return (T)defaultValue;
                }

                return default(T);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting default value for {SettingName}, using hardcoded default", settingName);

                if (DefaultPreferences.TryGetValue(settingName, out var defaultValue))
                {
                    return (T)defaultValue;
                }

                return default(T);
            }
        }

        private async Task<PreferenceValidationInfo> ValidatePreferencesAsync(UserPreference preference)
        {
            var validationInfo = new PreferenceValidationInfo { IsValid = true };

            try
            {
                var validationErrors = new List<string>();
                var warnings = new List<string>();

                // Validate language
                if (!IsValidLanguage(preference.Language))
                {
                    validationErrors.Add($"Invalid language: {preference.Language}");
                }

                // Validate response style
                if (!IsValidResponseStyle(preference.ResponseStyle))
                {
                    validationErrors.Add($"Invalid response style: {preference.ResponseStyle}");
                }

                // Validate tone
                if (!IsValidTone(preference.Tone))
                {
                    validationErrors.Add($"Invalid tone: {preference.Tone}");
                }

                // Validate max response length
                if (preference.MaxResponseLength < 50 || preference.MaxResponseLength > 2000)
                {
                    validationErrors.Add("Max response length must be between 50 and 2000 characters");
                }

                // Validate timezone
                if (!IsValidTimeZone(preference.TimeZone))
                {
                    warnings.Add($"Unknown timezone: {preference.TimeZone}");
                }

                // Validate theme
                if (!IsValidTheme(preference.Theme))
                {
                    validationErrors.Add($"Invalid theme: {preference.Theme}");
                }

                validationInfo.ValidationErrors = validationErrors;
                validationInfo.Warnings = warnings;
                validationInfo.IsValid = validationErrors.Count == 0;

                if (!validationInfo.IsValid)
                {
                    validationInfo.SuggestedValues = GetSuggestedValues();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating preferences for user {UserId}", preference.UserId);
                validationInfo.ValidationErrors.Add("Validation process failed");
                validationInfo.IsValid = false;
            }

            return validationInfo;
        }

        private async Task<PreferenceValidationInfo> ValidateUpdateRequestAsync(UpdatePreferencesRequest request)
        {
            var validationInfo = new PreferenceValidationInfo { IsValid = true };
            var validationErrors = new List<string>();

            // Validate each field in the request
            if (!string.IsNullOrEmpty(request.Language) && !IsValidLanguage(request.Language))
            {
                validationErrors.Add($"Invalid language: {request.Language}");
            }

            if (!string.IsNullOrEmpty(request.ResponseStyle) && !IsValidResponseStyle(request.ResponseStyle))
            {
                validationErrors.Add($"Invalid response style: {request.ResponseStyle}");
            }

            if (!string.IsNullOrEmpty(request.Tone) && !IsValidTone(request.Tone))
            {
                validationErrors.Add($"Invalid tone: {request.Tone}");
            }

            if (request.MaxResponseLength > 0 && (request.MaxResponseLength < 50 || request.MaxResponseLength > 2000))
            {
                validationErrors.Add("Max response length must be between 50 and 2000 characters");
            }

            if (!string.IsNullOrEmpty(request.Theme) && !IsValidTheme(request.Theme))
            {
                validationErrors.Add($"Invalid theme: {request.Theme}");
            }

            validationInfo.ValidationErrors = validationErrors;
            validationInfo.IsValid = validationErrors.Count == 0;

            return validationInfo;
        }

        private async Task UpdatePreferenceFieldsAsync(UserPreference preference, UpdatePreferencesRequest request)
        {
            if (!string.IsNullOrEmpty(request.Language))
                preference.Language = request.Language;

            if (!string.IsNullOrEmpty(request.ResponseStyle))
                preference.ResponseStyle = request.ResponseStyle;

            if (!string.IsNullOrEmpty(request.Tone))
                preference.Tone = request.Tone;

            if (request.MaxResponseLength > 0)
                preference.MaxResponseLength = request.MaxResponseLength;

            preference.IncludeCitations = request.IncludeCitations;
            preference.EnableSuggestions = request.EnableSuggestions;
            preference.EnableNotifications = request.EnableNotifications;

            if (!string.IsNullOrEmpty(request.TimeZone))
                preference.TimeZone = request.TimeZone;

            if (!string.IsNullOrEmpty(request.DateFormat))
                preference.DateFormat = request.DateFormat;

            if (!string.IsNullOrEmpty(request.Theme))
                preference.Theme = request.Theme;

            if (request.CustomSettings != null)
                preference.CustomSettings = JsonSerializer.Serialize(request.CustomSettings, _jsonOptions);

            if (request.PreferredTopics != null)
                preference.PreferredTopics = JsonSerializer.Serialize(request.PreferredTopics, _jsonOptions);

            if (request.BlockedTopics != null)
                preference.BlockedTopics = JsonSerializer.Serialize(request.BlockedTopics, _jsonOptions);
        }

        private async Task ResetToDefaultValuesAsync(UserPreference preference)
        {
            var systemPreferences = await GetSystemPreferencesAsync();

            preference.Language = GetDefaultValue<string>("Language", systemPreferences);
            preference.ResponseStyle = GetDefaultValue<string>("ResponseStyle", systemPreferences);
            preference.Tone = GetDefaultValue<string>("Tone", systemPreferences);
            preference.MaxResponseLength = GetDefaultValue<int>("MaxResponseLength", systemPreferences);
            preference.IncludeCitations = GetDefaultValue<bool>("IncludeCitations", systemPreferences);
            preference.EnableSuggestions = GetDefaultValue<bool>("EnableSuggestions", systemPreferences);
            preference.EnableNotifications = GetDefaultValue<bool>("EnableNotifications", systemPreferences);
            preference.TimeZone = GetDefaultValue<string>("TimeZone", systemPreferences);
            preference.DateFormat = GetDefaultValue<string>("DateFormat", systemPreferences);
            preference.Theme = GetDefaultValue<string>("Theme", systemPreferences);
            preference.CustomSettings = "{}";
            preference.PreferredTopics = "[]";
            preference.BlockedTopics = "[]";
        }

        private UserPreference ClonePreference(UserPreference preference)
        {
            return new UserPreference
            {
                Id = preference.Id,
                UserId = preference.UserId,
                Language = preference.Language,
                ResponseStyle = preference.ResponseStyle,
                Tone = preference.Tone,
                MaxResponseLength = preference.MaxResponseLength,
                IncludeCitations = preference.IncludeCitations,
                EnableSuggestions = preference.EnableSuggestions,
                EnableNotifications = preference.EnableNotifications,
                TimeZone = preference.TimeZone,
                DateFormat = preference.DateFormat,
                Theme = preference.Theme,
                CustomSettings = preference.CustomSettings,
                PreferredTopics = preference.PreferredTopics,
                BlockedTopics = preference.BlockedTopics,
                CreatedAt = preference.CreatedAt,
                UpdatedAt = preference.UpdatedAt,
                IsDeleted = preference.IsDeleted
            };
        }

        private bool IsDefaultPreference(UserPreference preference)
        {
            var systemDefaults = GetHardcodedDefaults();

            return preference.Language == systemDefaults["Language"].ToString() &&
                   preference.ResponseStyle == systemDefaults["ResponseStyle"].ToString() &&
                   preference.Tone == systemDefaults["Tone"].ToString() &&
                   preference.MaxResponseLength == (int)systemDefaults["MaxResponseLength"] &&
                   preference.IncludeCitations == (bool)systemDefaults["IncludeCitations"] &&
                   preference.EnableSuggestions == (bool)systemDefaults["EnableSuggestions"] &&
                   preference.EnableNotifications == (bool)systemDefaults["EnableNotifications"] &&
                   preference.TimeZone == systemDefaults["TimeZone"].ToString() &&
                   preference.DateFormat == systemDefaults["DateFormat"].ToString() &&
                   preference.Theme == systemDefaults["Theme"].ToString();
        }

        private Dictionary<string, object> ParseJsonToDictionary(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, object>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions) ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing JSON to dictionary: {Json}", json);
                return new Dictionary<string, object>();
            }
        }

        private List<string> ParseJsonToList(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing JSON to list: {Json}", json);
                return new List<string>();
            }
        }

        private bool IsValidLanguage(string language)
        {
            var supportedLanguages = new[] { "en", "es", "fr", "de", "it", "pt", "ru", "zh", "ja", "ko", "vi" };
            return supportedLanguages.Contains(language?.ToLower());
        }

        private bool IsValidResponseStyle(string style)
        {
            var validStyles = new[] { "concise", "balanced", "detailed" };
            return validStyles.Contains(style?.ToLower());
        }

        private bool IsValidTone(string tone)
        {
            var validTones = new[] { "formal", "professional", "friendly", "casual" };
            return validTones.Contains(tone?.ToLower());
        }

        private bool IsValidTimeZone(string timeZone)
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(timeZone);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsValidTheme(string theme)
        {
            var validThemes = new[] { "light", "dark", "auto" };
            return validThemes.Contains(theme?.ToLower());
        }

        private Dictionary<string, object> GetSuggestedValues()
        {
            return new Dictionary<string, object>
            {
                { "Language", new[] { "en", "es", "fr", "de", "vi" } },
                { "ResponseStyle", new[] { "concise", "balanced", "detailed" } },
                { "Tone", new[] { "formal", "professional", "friendly", "casual" } },
                { "Theme", new[] { "light", "dark", "auto" } },
                { "MaxResponseLength", new[] { 100, 250, 500, 1000, 1500 } }
            };
        }

        private Dictionary<string, object> GetHardcodedDefaults()
        {
            return new Dictionary<string, object>(DefaultPreferences);
        }

        private UserPreferenceResponse CreateHardcodedDefaultPreference()
        {
            return new UserPreferenceResponse
            {
                UserId = Guid.Empty,
                Language = "en",
                ResponseStyle = "balanced",
                Tone = "professional",
                MaxResponseLength = 500,
                IncludeCitations = true,
                EnableSuggestions = true,
                EnableNotifications = true,
                TimeZone = "UTC",
                DateFormat = "MM/dd/yyyy",
                Theme = "light",
                CustomSettings = new Dictionary<string, object>(),
                PreferredTopics = new List<string>(),
                BlockedTopics = new List<string>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
                IsDefault = true,
                ValidationInfo = new PreferenceValidationInfo { IsValid = true }
            };
        }
    }
}
