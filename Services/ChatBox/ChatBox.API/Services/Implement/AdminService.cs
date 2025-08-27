using System.Web;
using AutoMapper;
using ChatBox.API.Constants;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace ChatBox.API.Services.Implement
{
    public class AdminService : IAdminService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly ISemanticKernelService _kernelService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<AdminService> _logger;

        public AdminService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IMapper mapper,
            IConfiguration configuration,
            ISemanticKernelService semanticKernelService,
            ICacheService cacheService,
            ILogger<AdminService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _configuration = configuration;
            _kernelService = semanticKernelService;
            _cacheService = cacheService;
            _logger = logger;
        }
        #region AI Configuration Management

        public async Task<List<AIConfigurationResponse>> GetAIConfigurationsAsync()
        {
            try
            {
                var configs = await _unitOfWork.GetRepository<AIConfiguration>()
                      .GetListAsync(orderBy: q => q.OrderByDescending(c => c.IsActive)
                                                 .ThenByDescending(c => c.IsDefault)
                                                 .ThenBy(c => c.DisplayName));

                return _mapper.Map<List<AIConfigurationResponse>>(configs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get AI configurations");
                throw;
            }
        }

        public async Task<AIConfigurationResponse> CreateAIConfigurationAsync(AIConfigurationRequest request, string userId)
        {
            try
            {
                var decodedModelName = Uri.UnescapeDataString(request.ModelName ?? string.Empty);
                var normalizedModelName = NormalizeModelName(decodedModelName);

                // Check duplicate
                var existingConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.ModelName == normalizedModelName);

                if (existingConfig != null)
                    throw new ArgumentException(string.Format(MessageConstant.Admin.ModelExists, request.ModelName));

                var config = _mapper.Map<AIConfiguration>(request);
                config.ModelName = normalizedModelName; 
                config.CreatedBy = userId;
                config.UpdatedBy = userId;
                config.CreatedAt = DateTime.UtcNow;
                config.UpdatedAt = DateTime.UtcNow;
                config.IsActive = false; 
                config.IsDefault = false; 

                // Set system prompt
                if (string.IsNullOrEmpty(config.SystemPrompt))
                {
                    config.SystemPrompt = ChatConstants.SystemPrompt;
                }
                else if (config.SystemPrompt.Length > 5000)
                {
                    throw new ArgumentException("SystemPrompt quá dài. Vui lòng rút ngắn xuống dưới 5000 ký tự.");
                }

                await _unitOfWork.GetRepository<AIConfiguration>().InsertAsync(config);
                await _unitOfWork.CommitAsync();

                await ClearAllModelCaches();

                _logger.LogInformation("Created AI configuration: {ModelName} by {UserId}", request.ModelName, userId);

                return _mapper.Map<AIConfigurationResponse>(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create AI configuration for model: {ModelName}", request?.ModelName);
                throw;
            }
        }
        public async Task<AIConfigurationResponse> UpdateAIConfigurationAsync(string id, AIConfigurationRequest request, string userId)
        {
            try
            {
                var config = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: x => x.Id == id);

                if (config == null)
                    throw new ArgumentException(MessageConstant.Admin.ConfigNotFound);

                // ✅ FIX: Add URL decoding for update too
                var decodedModelName = Uri.UnescapeDataString(request.ModelName ?? string.Empty);
                var normalizedModelName = NormalizeModelName(decodedModelName);

                if (normalizedModelName != config.ModelName && config.IsActive)
                {
                    throw new InvalidOperationException("Không thể thay đổi tên model khi model đang được kích hoạt. Vui lòng tắt model trước khi chỉnh sửa.");
                }
                if (normalizedModelName != config.ModelName)
                {
                    var duplicate = await _unitOfWork.GetRepository<AIConfiguration>()
                        .SingleOrDefaultAsync(predicate: c => c.ModelName == normalizedModelName && c.Id != id);

                    if (duplicate != null)
                        throw new ArgumentException(string.Format(MessageConstant.Admin.ModelExists, decodedModelName)); // ✅ FIX: Use decoded
                }

                var oldModelName = config.ModelName;
                var wasActive = config.IsActive;

                _mapper.Map(request, config);
                config.ModelName = normalizedModelName;
                config.UpdatedAt = DateTime.UtcNow;
                config.UpdatedBy = userId;

                // ✅ Validate system prompt
                if (string.IsNullOrEmpty(config.SystemPrompt))
                {
                    config.SystemPrompt = ChatConstants.SystemPrompt;
                }
                else if (config.SystemPrompt.Length > 5000)
                {
                    throw new ArgumentException("SystemPrompt quá dài. Vui lòng rút ngắn xuống dưới 5000 ký tự.");
                }

                _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(config);
                await _unitOfWork.CommitAsync();

                // ✅ Clear caches if important changes
                if (wasActive || oldModelName != config.ModelName)
                {
                    await ClearAllModelCaches(oldModelName);
                    await ClearAllModelCaches(config.ModelName);
                }

                _logger.LogInformation("Updated AI configuration: {ConfigId} by {UserId}", id, userId);

                return _mapper.Map<AIConfigurationResponse>(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update AI configuration: {ConfigId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteAIConfigurationAsync(string id)
        {
            try
            {
                var config = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: x => x.Id == id);

                if (config == null)
                    return false;

                // Kiểm tra cơ bản
                var totalConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                    .GetListAsync();

                if (totalConfigs.Count <= 1)
                    throw new InvalidOperationException(MessageConstant.Admin.CannotDeleteLastConfig);

                if (config.IsActive)
                {
                    throw new InvalidOperationException("Không thể xóa model đang active. Vui lòng tắt model trước khi xóa.");
                }

                // ✅ LUÔN LUÔN auto migrate - bỏ parameter autoMigrate
                var activeSessions = await _unitOfWork.GetRepository<ChatSession>()
                    .GetListAsync(predicate: s => s.ModelName == config.ModelName && s.IsActive);

                if (activeSessions.Any())
                {
                    // Tìm model thay thế tốt nhất
                    var replacementModel = await FindBestReplacementModel(config.Id);

                    if (replacementModel != null)
                    {
                        _logger.LogInformation("Auto-migrating {Count} sessions from {OldModel} to {NewModel}",
                            activeSessions.Count, config.ModelName, replacementModel.ModelName);

                        // Migrate tất cả sessions
                        foreach (var session in activeSessions)
                        {
                            session.ModelName = replacementModel.ModelName;
                            session.UpdatedAt = DateTime.UtcNow;
                            session.UpdatedBy = "system";

                            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
                        }

                        _logger.LogInformation("Auto-migrated {Count} sessions successfully", activeSessions.Count);
                    }
                    else
                    {
                        // ✅ FALLBACK: Nếu không có replacement, deactivate sessions thay vì error
                        _logger.LogWarning("No replacement model found, deactivating {Count} sessions", activeSessions.Count);

                        foreach (var session in activeSessions)
                        {
                            session.IsActive = false;
                            session.UpdatedAt = DateTime.UtcNow;
                            session.UpdatedBy = "system";
                            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
                        }
                    }
                }

                // Proceed với deletion
                _unitOfWork.GetRepository<AIConfiguration>().DeleteAsync(config);
                await _unitOfWork.CommitAsync();

                await ClearAllModelCaches(config.ModelName);

                _logger.LogInformation("Deleted AI configuration: {ConfigId} - {ModelName}", id, config.ModelName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete AI configuration: {ConfigId}", id);
                throw;
            }
        }
        private async Task<AIConfiguration> FindBestReplacementModel(string excludeConfigId)
        {
            var activeConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                .GetListAsync(predicate: c => c.IsActive && c.Id != excludeConfigId);

            if (!activeConfigs.Any())
                return null;

            // Priority 1: Default model
            var defaultModel = activeConfigs.FirstOrDefault(c => c.IsDefault);
            if (defaultModel != null)
                return defaultModel;

            // Priority 2: Free model
            var freeModel = activeConfigs.FirstOrDefault(c => c.IsFree);
            if (freeModel != null)
                return freeModel;

            // Priority 3: First available
            return activeConfigs.First();
        }
        public async Task<ModelActivationResponse> TestAndActivateModelByIdAsync(string configId, string userId)
        {
            try
            {
                var targetConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.Id == configId);

                if (targetConfig == null)
                    return new ModelActivationResponse
                    {
                        Success = false,
                        Error = string.Format(MessageConstant.Admin.ConfigNotFound)
                    };

                _logger.LogInformation("Testing model {ModelName} (ID: {ConfigId}) before activation",
                    targetConfig.ModelName, configId);

                var testResult = await _kernelService.TestModelAsync(targetConfig.ModelName);

                if (!testResult.Success)
                    return new ModelActivationResponse
                    {
                        Success = false,
                        Error = $"Model test failed: {testResult.Error}",
                        TestResponse = testResult.Response,
                        ResponseTimeMs = (int)testResult.ResponseTimeMs
                    };

                // Test passed → Activate
                bool wasAlreadyActive = targetConfig.IsActive;

                if (!targetConfig.IsActive)
                {
                    targetConfig.IsActive = true;
                    targetConfig.UpdatedAt = DateTime.UtcNow;
                    targetConfig.UpdatedBy = userId;

                    _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(targetConfig);
                    await _unitOfWork.CommitAsync();

                    await ClearAllModelCaches(targetConfig.ModelName);
                }

                _logger.LogInformation("Model {ModelName} (ID: {ConfigId}) tested successfully and {Action} by {UserId}",
                    targetConfig.ModelName, configId, wasAlreadyActive ? "confirmed active" : "activated", userId);

                return new ModelActivationResponse
                {
                    Success = true,
                    ModelName = targetConfig.ModelName,
                    TestResponse = testResult.Response,
                    ResponseTimeMs = (int)testResult.ResponseTimeMs,
                    Message = wasAlreadyActive
                        ? "Model test successful (already active)"
                        : "Model tested and activated successfully",
                    WasAlreadyActive = wasAlreadyActive
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test and activate model by ID {ConfigId}", configId);
                return new ModelActivationResponse
                {
                    Success = false,
                    Error = $"System error: {ex.Message}"
                };
            }
        }

        public async Task<(bool Success, string Message)> DeactivateModelByIdAsync(string configId, string userId)
        {
            try
            {
                var targetConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.Id == configId);

                if (targetConfig == null || !targetConfig.IsActive)
                    return (false, "Model không tồn tại hoặc đã bị tắt");

                // Safety: Don't allow deactivating last active model
                var otherActiveConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                    .GetListAsync(predicate: c => c.IsActive && c.Id != targetConfig.Id);

                if (!otherActiveConfigs.Any())
                    throw new InvalidOperationException("Không thể tắt model cuối cùng. Hệ thống cần ít nhất 1 model active.");

                // ✅ NEW: Migrate sessions trước khi deactivate
                var activeSessions = await _unitOfWork.GetRepository<ChatSession>()
                    .GetListAsync(predicate: s => s.ModelName == targetConfig.ModelName && s.IsActive);

                if (activeSessions.Any())
                {
                    // Tìm model thay thế tốt nhất (từ models còn active)
                    var replacementModel = await FindBestReplacementModel(targetConfig.Id);

                    if (replacementModel != null)
                    {
                        _logger.LogInformation("Migrating {Count} sessions from deactivated model {OldModel} to {NewModel}",
                            activeSessions.Count, targetConfig.ModelName, replacementModel.ModelName);

                        // Migrate tất cả sessions
                        foreach (var session in activeSessions)
                        {
                            session.ModelName = replacementModel.ModelName;
                            session.UpdatedAt = DateTime.UtcNow;
                            session.UpdatedBy = userId;

                            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
                        }

                        _logger.LogInformation("Migrated {Count} sessions successfully", activeSessions.Count);
                    }
                    else
                    {
                        // ✅ FALLBACK: Nếu không có replacement, deactivate sessions
                        _logger.LogWarning("No replacement model found, deactivating {Count} sessions", activeSessions.Count);

                        foreach (var session in activeSessions)
                        {
                            session.IsActive = false;
                            session.UpdatedAt = DateTime.UtcNow;
                            session.UpdatedBy = userId;
                            _unitOfWork.GetRepository<ChatSession>().UpdateAsync(session);
                        }
                    }
                }

                // If this is default model, set another active model as default
                if (targetConfig.IsDefault && otherActiveConfigs.Any())
                {
                    var newDefault = otherActiveConfigs.First();
                    newDefault.IsDefault = true;
                    newDefault.UpdatedAt = DateTime.UtcNow;
                    newDefault.UpdatedBy = userId;
                    _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(newDefault);

                    _logger.LogInformation("Model {ModelName} set as new default", newDefault.ModelName);
                }

                // ✅ Deactivate model
                targetConfig.IsActive = false;
                targetConfig.IsDefault = false;
                targetConfig.UpdatedAt = DateTime.UtcNow;
                targetConfig.UpdatedBy = userId;

                _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(targetConfig);
                await _unitOfWork.CommitAsync();

                await ClearAllModelCaches(targetConfig.ModelName);

                _logger.LogInformation("Model {ModelName} (ID: {ConfigId}) deactivated with session migration by {UserId}",
                    targetConfig.ModelName, configId, userId);

                var sessionInfo = activeSessions.Any()
                    ? $" và đã migrate {activeSessions.Count} sessions"
                    : "";

                return (true, $"Model '{targetConfig.DisplayName}' đã được tắt{sessionInfo}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deactivate model by ID: {ConfigId}", configId);
                throw;
            }
        }


        public async Task<(bool Success, string Message)> SetDefaultModelByIdAsync(string configId, string userId)
        {
            try
            {
                var targetConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.Id == configId);

                if (targetConfig == null)
                    return (false, string.Format(MessageConstant.Admin.ConfigNotFound));

                if (!targetConfig.IsActive)
                    return (false, $"Model '{targetConfig.DisplayName}' phải được kích hoạt trước khi đặt làm mặc định");

                if (targetConfig.IsDefault)
                    return (true, $"Model '{targetConfig.DisplayName}' đã là mặc định"); // Already default

                // Clear current default
                var currentDefault = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.IsDefault);

                if (currentDefault != null)
                {
                    currentDefault.IsDefault = false;
                    currentDefault.UpdatedAt = DateTime.UtcNow;
                    currentDefault.UpdatedBy = userId;
                    _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(currentDefault);
                }

                // Set new default
                targetConfig.IsDefault = true;
                targetConfig.UpdatedAt = DateTime.UtcNow;
                targetConfig.UpdatedBy = userId;

                _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(targetConfig);
                await _unitOfWork.CommitAsync();

                await ClearAllModelCaches();

                _logger.LogInformation("Model {ModelName} (ID: {ConfigId}) set as default by {UserId}",
                    targetConfig.ModelName, configId, userId);

                return (true, $"Model '{targetConfig.DisplayName}' đã được đặt làm mặc định");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set default model by ID {ConfigId}", configId);
                throw;
            }
        }
        public async Task<bool> SetMultipleActiveModelsAsync(List<string> modelNames, string userId)
        {
            try
            {
                if (modelNames == null || !modelNames.Any())
                    throw new ArgumentException("Cần ít nhất 1 model được chọn.");

                // ✅ Normalize đầu vào
                var normalizedNames = modelNames
                    .Select(NormalizeModelName)
                    .Distinct()
                    .ToList();

                var allConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                    .GetListAsync();

                var targetConfigs = allConfigs
                    .Where(c => c.ModelName != null && normalizedNames.Contains(c.ModelName))
                    .ToList();

                if (targetConfigs.Count != normalizedNames.Count)
                    throw new ArgumentException("Một số model không tồn tại trong hệ thống.");

                // ✅ Cập nhật trạng thái active
                foreach (var config in allConfigs)
                {
                    var shouldBeActive = targetConfigs.Any(t => t.Id == config.Id);
                    if (config.IsActive != shouldBeActive)
                    {
                        config.IsActive = shouldBeActive;
                        config.UpdatedAt = DateTime.UtcNow;
                        config.UpdatedBy = userId;

                        // Gọi UpdateAsync (nhưng không await từng cái — xử lý sau)
                        _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(config);
                    }
                }

                await _unitOfWork.CommitAsync();
                await ClearModelCaches();

                _logger.LogInformation("Bulk updated models by {UserId}: {Models}",
                    userId, string.Join(", ", modelNames));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set multiple active models");
                throw;
            }
        }

        private async Task<bool> IsConfigurationInUseAsync(string modelName)
        {
            var normalized = NormalizeModelName(modelName);
            var activeSessions = await _unitOfWork.GetRepository<ChatSession>()
                .GetListAsync(predicate: s => s.ModelName == normalized && s.IsActive);

            return activeSessions.Any();
        }

        #endregion

        #region Statistics

        public async Task<SystemStatisticsResponse> GetSystemStatisticsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var last30Days = now.AddDays(-30);

                var totalSessions = await _unitOfWork.GetRepository<ChatSession>().GetListAsync();
                var totalMessages = await _unitOfWork.GetRepository<ChatMessage>().GetListAsync();

                var uniqueUsers = totalSessions.Select(s => s.UserId).Distinct().Count();
                var activeSessions = totalSessions.Where(s => s.LastActiveAt >= last30Days).Count();

                var modelUsage = totalSessions
                    .GroupBy(s => s.ModelName)
                    .Select(g => new ModelUsageStatistics
                    {
                        ModelName = g.Key,
                        SessionCount = g.Count(),
                        MessageCount = g.SelectMany(s => s.Messages).Count(),
                        LastUsed = g.Max(s => s.LastActiveAt)
                    })
                    .OrderByDescending(m => m.SessionCount)
                    .ToList();

                return new SystemStatisticsResponse
                {
                    TotalSessions = totalSessions.Count,
                    TotalMessages = totalMessages.Count,
                    TotalUsers = uniqueUsers,
                    ActiveSessions = activeSessions,
                    ModelUsageStats = modelUsage,
                    GeneratedAt = now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system statistics");
                throw;
            }
        }

        public async Task<List<DailyActivityResponse>> GetDailyActivityAsync(int days = 30)
        {
            try
            {
                var endDate = DateTime.UtcNow.Date;
                var startDate = endDate.AddDays(-days);

                var messages = await _unitOfWork.GetRepository<ChatMessage>()
                    .GetListAsync(predicate: m => m.CreatedAt >= startDate && m.CreatedAt <= endDate.AddDays(1));

                var sessions = await _unitOfWork.GetRepository<ChatSession>()
                    .GetListAsync(predicate: s => s.CreatedAt >= startDate && s.CreatedAt <= endDate.AddDays(1));

                var dailyStats = new List<DailyActivityResponse>();

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    var dayMessages = messages.Where(m => m.CreatedAt.Date == date).ToList();
                    var daySessions = sessions.Where(s => s.CreatedAt.Date == date).ToList();

                    dailyStats.Add(new DailyActivityResponse
                    {
                        Date = date,
                        MessageCount = dayMessages.Count,
                        SessionCount = daySessions.Count,
                        UniqueUsers = daySessions.Select(s => s.UserId).Distinct().Count(),
                        TokensUsed = dayMessages.Sum(m => m.TokenCount)
                    });
                }

                return dailyStats.OrderBy(d => d.Date).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get daily activity");
                throw;
            }
        }

        public async Task<List<ModelUsageStatistics>> GetModelUsageStatisticsAsync()
        {
            try
            {
                var sessions = await _unitOfWork.GetRepository<ChatSession>()
                    .GetListAsync(include: q => q.Include(x => x.Messages));

                var modelStats = sessions
                    .GroupBy(s => s.ModelName)
                    .Select(g => new ModelUsageStatistics
                    {
                        ModelName = g.Key,
                        SessionCount = g.Count(),
                        MessageCount = g.SelectMany(s => s.Messages).Count(),
                        TokensUsed = g.SelectMany(s => s.Messages).Sum(m => m.TokenCount),
                        UniqueUsers = g.Select(s => s.UserId).Distinct().Count(),
                        LastUsed = g.Max(s => s.LastActiveAt),
                        AverageSessionLength = g.Average(s => s.Messages.Count)
                    })
                    .OrderByDescending(m => m.SessionCount)
                    .ToList();

                return modelStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get model usage statistics");
                throw;
            }
        }

        #endregion

        #region Model Testing

        public async Task<ModelTestResponse> TestModelByIdAsync(string configId, string userId)
        {
            try
            {
                var targetConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.Id == configId);

                if (targetConfig == null)
                {
                    return new ModelTestResponse
                    {
                        Success = false,
                        Error = string.Format(MessageConstant.Admin.ConfigNotFound),
                        TestTime = DateTime.UtcNow
                    };
                }

                var testResult = await _kernelService.TestModelAsync(targetConfig.ModelName);

                _logger.LogInformation("Model test completed: {ModelName} (ID: {ConfigId}), Success: {Success}",
                    targetConfig.ModelName, configId, testResult.Success);

                return new ModelTestResponse
                {
                    Success = testResult.Success,
                    Error = testResult.Error,
                    ResponseTime = (int)testResult.ResponseTimeMs,
                    TestMessage = "Test connection to OpenRouter",
                    Response = testResult.Response,
                    TestTime = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test model by ID: {ConfigId}", configId);
                return new ModelTestResponse
                {
                    Success = false,
                    Error = $"System error: {ex.Message}",
                    TestTime = DateTime.UtcNow
                };
            }
        }

        #endregion

            #region Model Impact Analysis

        public async Task<ModelImpactResponse> GetModelImpactAnalysisAsync(string modelName)
        {
            try
            {
                var normalizedModelName = NormalizeModelName(modelName);

                var activeSessions = await _unitOfWork.GetRepository<ChatSession>()
                    .GetListAsync(
                        predicate: s => s.ModelName == normalizedModelName && s.IsActive,
                        include: q => q.Include(s => s.Messages));

                var affectedUsers = activeSessions.Select(s => s.UserId).Distinct().Count();
                var lastUsed = activeSessions.Any()
                    ? activeSessions.Max(s => s.LastActiveAt)
                    : DateTime.MinValue;

                var canSafelyDeactivate = !activeSessions.Any() ||
                    activeSessions.All(s => s.LastActiveAt < DateTime.UtcNow.AddDays(-7));

                var impact = activeSessions.Count switch
                {
                    0 => "Không có ảnh hưởng",
                    var count when count <= 5 => "Ảnh hưởng thấp",
                    var count when count <= 20 => "Ảnh hưởng trung bình",
                    _ => "Ảnh hưởng cao"
                };

                var recommendations = new List<string>();
                if (!canSafelyDeactivate)
                {
                    recommendations.Add("Thông báo trước cho users đang sử dụng");
                    recommendations.Add("Đề xuất users chuyển sang model khác");
                }
                if (activeSessions.Any(s => s.LastActiveAt > DateTime.UtcNow.AddHours(-1)))
                {
                    recommendations.Add("Có users đang chat active - nên đợi lúc khác");
                }

                return new ModelImpactResponse
                {
                    ModelName = modelName,
                    ActiveSessionsCount = activeSessions.Count,
                    AffectedUsersCount = affectedUsers,
                    LastUsed = lastUsed,
                    CanSafelyDeactivate = canSafelyDeactivate,
                    Impact = impact,
                    Recommendations = recommendations
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get model impact analysis: {ModelName}", modelName);
                throw;
            }
        }

        #endregion

        #region Helper Methods
        private async Task ClearAllModelCaches(string modelName = null)
        {
            var cacheKeys = new List<string>
        {
            "active_models_cache",
            "default_active_model"
        };

            // 🔧 CRITICAL: Clear individual model cache keys
            if (!string.IsNullOrEmpty(modelName))
            {
                var normalizedModelName = NormalizeModelName(modelName);
                cacheKeys.AddRange(new[]
                {
                $"model_active_{normalizedModelName}",
                $"model_valid_{normalizedModelName}",
                $"model_active_{modelName}",  // Original name too
                $"model_valid_{modelName}"    // Original name too
            });
            }
            else
            {
                // 🔧 If no specific model, get all models and clear their caches
                var allConfigs = await _unitOfWork.GetRepository<AIConfiguration>().GetListAsync();
                foreach (var config in allConfigs)
                {
                    var normalized = NormalizeModelName(config.ModelName);
                    cacheKeys.AddRange(new[]
                    {
                    $"model_active_{normalized}",
                    $"model_valid_{normalized}",
                    $"model_active_{config.ModelName}",
                    $"model_valid_{config.ModelName}"
                });
                }
            }

            // Remove duplicates
            cacheKeys = cacheKeys.Distinct().ToList();

            _logger.LogInformation("Clearing {Count} cache keys for model operations", cacheKeys.Count);

            var clearTasks = cacheKeys.Select(key => _cacheService.RemoveAsync(key));

            try
            {
                await Task.WhenAll(clearTasks);
                _logger.LogInformation("Successfully cleared all model caches");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear some model caches");
            }
        }
        private async Task ClearModelCaches()
        {
            var cacheKeys = new[]
            {
                "active_models_cache",
                "default_active_model"
            };

            var clearTasks = cacheKeys.Select(key => _cacheService.RemoveAsync(key));

            try
            {
                await Task.WhenAll(clearTasks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear some model caches");
            }
        }

        private string NormalizeModelName(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return string.Empty;

            // ✅ DON'T double-decode if already decoded
            // Just normalize the already decoded string
            return modelName.Trim().ToLowerInvariant();
        }
        #endregion
    }
}
