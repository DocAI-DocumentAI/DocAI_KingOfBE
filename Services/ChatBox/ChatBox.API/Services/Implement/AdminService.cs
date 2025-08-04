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
        public AdminService(
        IUnitOfWork<ChatBoxDbContext> unitOfWork,
        IMapper mapper,
        IConfiguration configuration,
        ISemanticKernelService semanticKernelService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _configuration = configuration;
            _kernelService = semanticKernelService;
        }
        #region AI Configuration Management

        public async Task<List<AIConfigurationResponse>> GetAIConfigurationsAsync()
        {
            var configs = await _unitOfWork.GetRepository<AIConfiguration>()
                .GetListAsync(orderBy: q => q.OrderByDescending(c => c.IsActive).ThenBy(c => c.DisplayName));

            return _mapper.Map<List<AIConfigurationResponse>>(configs);
        }
        public async Task<AIConfigurationResponse> CreateAIConfigurationAsync(AIConfigurationRequest request, string userId)
        {
            var normalizedModelName = NormalizeModelName(request.ModelName);

            // Inline validation
            var existingConfig = await _unitOfWork.GetRepository<AIConfiguration>()
               .SingleOrDefaultAsync(predicate: c => NormalizeModelName(c.ModelName) == normalizedModelName);

            if (existingConfig != null)
                throw new ArgumentException(string.Format(MessageConstant.Admin.ModelExists, request.ModelName));

            var config = _mapper.Map<AIConfiguration>(request);
            config.CreatedBy = userId;
            config.UpdatedBy = userId;
            config.CreatedAt = DateTime.UtcNow;
            config.UpdatedAt = DateTime.UtcNow;

            // Set system prompt from config if not provided
            if (string.IsNullOrEmpty(config.SystemPrompt))
            {
                config.SystemPrompt = ChatConstants.SystemPrompt;
            }
            else
            {
                // Validate custom system prompt
                if (config.SystemPrompt.Length > 5000) // reasonable limit
                {
                    throw new ArgumentException("SystemPrompt quá dài. Vui lòng rút ngắn xuống dưới 5000 ký tự.");
                }

            }

            // Auto-activate first model
            var hasActiveConfig = await _unitOfWork.GetRepository<AIConfiguration>()
               .SingleOrDefaultAsync(predicate: c => c.IsActive) != null;

            if (!hasActiveConfig)
            {
                config.IsActive = true;
            }

            await _unitOfWork.GetRepository<AIConfiguration>().InsertAsync(config);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AIConfigurationResponse>(config);
        }


        public async Task<AIConfigurationResponse> UpdateAIConfigurationAsync(string id, AIConfigurationRequest request, string userId)
        {
            var config = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: x => x.Id == id);

            if (config == null)
                throw new ArgumentException(MessageConstant.Admin.ConfigNotFound);

            var normalizedModelName = NormalizeModelName(request.ModelName);

            // Inline validation for duplicate model name
            if (normalizedModelName != NormalizeModelName(config.ModelName))
            {
                var duplicate = await _unitOfWork.GetRepository<AIConfiguration>()
                   .SingleOrDefaultAsync(predicate: c => NormalizeModelName(c.ModelName) == normalizedModelName && c.Id != id);

                if (duplicate != null)
                    throw new ArgumentException(string.Format(MessageConstant.Admin.ModelExists, request.ModelName));
            }
            var oldSystemPrompt = config.SystemPrompt;


            _mapper.Map(request, config);
            config.UpdatedAt = DateTime.UtcNow;
            config.UpdatedBy = userId;

            if (config.SystemPrompt != oldSystemPrompt)
            {
                if (string.IsNullOrEmpty(config.SystemPrompt))
                {
                    config.SystemPrompt = ChatConstants.SystemPrompt;
                }
                else if (config.SystemPrompt.Length > 5000)
                {
                    throw new ArgumentException("SystemPrompt quá dài. Vui lòng rút ngắn xuống dưới 5000 ký tự.");
                }
                else
                {
                    Console.WriteLine($"🎯 Updated SystemPrompt for model: {config.ModelName} (Length: {config.SystemPrompt.Length})");
                }
            }

            _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(config);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AIConfigurationResponse>(config);
        }

        public async Task<bool> DeleteAIConfigurationAsync(string id)
        {
            var config = await _unitOfWork.GetRepository<AIConfiguration>().SingleOrDefaultAsync(predicate: x => x.Id == id);
            if (config == null)
                return false;

            if (config.IsActive)
            {
                var totalConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                    .GetListAsync();

                if (totalConfigs.Count <= 1)
                    throw new InvalidOperationException("Không thể xóa cấu hình AI duy nhất trong hệ thống.");

                throw new InvalidOperationException("Không thể xóa cấu hình đang active. Hãy active model khác trước.");
            }

            var isInUse = await IsConfigurationInUseAsync(config.ModelName);
            if (isInUse)
                throw new InvalidOperationException($"Không thể xóa model '{config.ModelName}' vì đang được sử dụng trong các phiên chat.");

            _unitOfWork.GetRepository<AIConfiguration>().DeleteAsync(config);
            await _unitOfWork.CommitAsync();

            return true;
        }

        public async Task<bool> SetActiveModelAsync(string modelName, string userId)
        {
            var normalizedModelName = NormalizeModelName(modelName);

            var allConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
                .GetListAsync(); // ← EF sẽ dịch được truy vấn này

            var targetConfig = allConfigs
                .FirstOrDefault(c => NormalizeModelName(c.ModelName) == normalizedModelName);

            if (targetConfig == null)
                return false;

            foreach (var config in allConfigs)
            {
                var wasActive = config.IsActive;
                config.IsActive = config.Id == targetConfig.Id;

                if (wasActive != config.IsActive)
                {
                    config.UpdatedAt = DateTime.UtcNow;
                    config.UpdatedBy = userId;
                     _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(config);
                }
            }

            await _unitOfWork.CommitAsync();
            return true;
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
        public async Task<List<DailyActivityResponse>> GetDailyActivityAsync(int days = 30)
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
        public async Task<List<ModelUsageStatistics>> GetModelUsageStatisticsAsync()
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
        #endregion

        #region Model Testing

   public async Task<ModelTestResponse> TestModelAsync(string modelName, string userId)
        {
            try
            {
                modelName = HttpUtility.UrlDecode(modelName);

        var normalizedModelName = NormalizeModelName(modelName);

        var allConfigs = await _unitOfWork.GetRepository<AIConfiguration>()
            .GetListAsync();

        var targetConfig = allConfigs
            .FirstOrDefault(c => NormalizeModelName(c.ModelName) == normalizedModelName);


                if (targetConfig == null)
                {
                    return new ModelTestResponse
                    {
                        Success = false,
                        Error = MessageConstant.Admin.ModelNotFound,
                        TestTime = DateTime.UtcNow
                    };
                }

                var testResult = await _kernelService.TestModelAsync(modelName);

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
                return new ModelTestResponse
                {
                    Success = false,
                    Error = $"System error: {ex.Message}",
                    TestTime = DateTime.UtcNow
                };
            }
        }

        #endregion

        private string NormalizeModelName(string modelName) =>
    Uri.UnescapeDataString(modelName).Trim().ToLowerInvariant();
    }
}