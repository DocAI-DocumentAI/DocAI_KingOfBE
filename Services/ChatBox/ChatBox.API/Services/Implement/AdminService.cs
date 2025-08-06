using System.Web;
using AutoMapper;
using ChatBox.API.Constants;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
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
                    .GetListAsync(orderBy: q => q.OrderByDescending(c => c.IsActive).ThenBy(c => c.DisplayName));

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
                var normalizedModelName = NormalizeModelName(request.ModelName);

                // Check duplicate
                var existingConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                   .SingleOrDefaultAsync(predicate: c => NormalizeModelName(c.ModelName) == normalizedModelName);

                if (existingConfig != null)
                    throw new ArgumentException(string.Format(MessageConstant.Admin.ModelExists, request.ModelName));

                var config = _mapper.Map<AIConfiguration>(request);
                config.CreatedBy = userId;
                config.UpdatedBy = userId;
                config.CreatedAt = DateTime.UtcNow;
                config.UpdatedAt = DateTime.UtcNow;

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

                // Clear cache
                await ClearModelCaches();

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

                var normalizedModelName = NormalizeModelName(request.ModelName);

                // Check duplicate (excluding current)
                if (normalizedModelName != NormalizeModelName(config.ModelName))
                {
                    var duplicate = await _unitOfWork.GetRepository<AIConfiguration>()
                       .SingleOrDefaultAsync(predicate: c => NormalizeModelName(c.ModelName) == normalizedModelName && c.Id != id);

                    if (duplicate != null)
                        throw new ArgumentException(string.Format(MessageConstant.Admin.ModelExists, request.ModelName));
                }

                var oldModelName = config.ModelName;
                var wasActive = config.IsActive;

                _mapper.Map(request, config);
                config.UpdatedAt = DateTime.UtcNow;
                config.UpdatedBy = userId;

                // Validate system prompt
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

                // Clear relevant caches
                if (wasActive || oldModelName != config.ModelName)
                {
                    await ClearModelCaches();
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

                // Check if it's the last configuration
                var totalConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                    .GetListAsync();

                if (totalConfigs.Count <= 1)
                    throw new InvalidOperationException(MessageConstant.Admin.CannotDeleteLastConfig);

                // Check if active and prevent deletion if it's the only active one
                if (config.IsActive)
                {
                    var otherActiveConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                        .GetListAsync(predicate: c => c.IsActive && c.Id != id);

                    if (!otherActiveConfigs.Any())
                        throw new InvalidOperationException(MessageConstant.Admin.CannotDeleteActiveConfig);
                }

                // Check if model is in use
                var isInUse = await IsConfigurationInUseAsync(config.ModelName);
                if (isInUse)
                    throw new InvalidOperationException(string.Format(MessageConstant.Admin.ModelInUse, config.ModelName));

                _unitOfWork.GetRepository<AIConfiguration>().DeleteAsync(config);
                await _unitOfWork.CommitAsync();

                await ClearModelCaches();

                _logger.LogInformation("Deleted AI configuration: {ConfigId} - {ModelName}", id, config.ModelName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete AI configuration: {ConfigId}", id);
                throw;
            }
        }

        public async Task<bool> SetActiveModelAsync(string modelName, string userId)
        {
            try
            {
                var normalizedModelName = NormalizeModelName(modelName);

                var targetConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => NormalizeModelName(c.ModelName) == normalizedModelName);

                if (targetConfig == null)
                    return false;

                // Toggle logic - if already active, deactivate; if inactive, activate
                var newActiveState = !targetConfig.IsActive;

                // Safety check - ensure at least one model remains active
                if (!newActiveState) // trying to deactivate
                {
                    var otherActiveConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                        .GetListAsync(predicate: c => c.IsActive && c.Id != targetConfig.Id);

                    if (!otherActiveConfigs.Any())
                        throw new InvalidOperationException("Không thể tắt model active cuối cùng. Hệ thống cần ít nhất 1 model active.");
                }

                targetConfig.IsActive = newActiveState;
                targetConfig.UpdatedAt = DateTime.UtcNow;
                targetConfig.UpdatedBy = userId;

                _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(targetConfig);
                await _unitOfWork.CommitAsync();

                await ClearModelCaches();

                _logger.LogInformation("Model {ModelName} {Action} by {UserId}",
                    modelName, newActiveState ? "activated" : "deactivated", userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle model active state: {ModelName}", modelName);
                throw;
            }
        }

        public async Task<bool> SetMultipleActiveModelsAsync(List<string> modelNames, string userId)
        {
            try
            {
                if (!modelNames?.Any() == true)
                    throw new ArgumentException("Cần ít nhất 1 model được chọn.");

                var normalizedNames = modelNames.Select(NormalizeModelName).ToList();

                var allConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                    .GetListAsync();

                var targetConfigs = allConfigs
                    .Where(c => normalizedNames.Contains(NormalizeModelName(c.ModelName)))
                    .ToList();

                if (targetConfigs.Count != modelNames.Count)
                    throw new ArgumentException("Một số model không tồn tại trong hệ thống.");

                // Update all configs - activate selected, deactivate others
                foreach (var config in allConfigs)
                {
                    var shouldBeActive = targetConfigs.Any(t => t.Id == config.Id);
                    if (config.IsActive != shouldBeActive)
                    {
                        config.IsActive = shouldBeActive;
                        config.UpdatedAt = DateTime.UtcNow;
                        config.UpdatedBy = userId;
                        _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(config);
                    }
                }

                await _unitOfWork.CommitAsync();
                await ClearModelCaches();

                _logger.LogInformation("Bulk updated models by {UserId}: {Models}", userId, string.Join(", ", modelNames));

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
                .GetListAsync(predicate: s => NormalizeModelName(s.ModelName) == normalized && s.IsActive);

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

        public async Task<ModelTestResponse> TestModelAsync(string modelName, string userId)
        {
            try
            {
                modelName = HttpUtility.UrlDecode(modelName);
                var normalizedModelName = NormalizeModelName(modelName);

                var targetConfig = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => NormalizeModelName(c.ModelName) == normalizedModelName);

                if (targetConfig == null)
                {
                    return new ModelTestResponse
                    {
                        Success = false,
                        Error = string.Format(MessageConstant.Admin.ModelNotFound, modelName),
                        TestTime = DateTime.UtcNow
                    };
                }

                var testResult = await _kernelService.TestModelAsync(modelName);

                _logger.LogInformation("Model test completed: {ModelName}, Success: {Success}",
                    modelName, testResult.Success);

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
                _logger.LogError(ex, "Failed to test model: {ModelName}", modelName);
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
                        predicate: s => NormalizeModelName(s.ModelName) == normalizedModelName && s.IsActive,
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

        private string NormalizeModelName(string modelName) =>
            Uri.UnescapeDataString(modelName ?? string.Empty).Trim().ToLowerInvariant();

        #endregion
    }
}
