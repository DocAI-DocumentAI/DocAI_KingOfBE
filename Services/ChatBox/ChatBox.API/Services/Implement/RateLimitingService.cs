using ChatBox.API.Services.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using System.Collections.Concurrent;

namespace ChatBox.API.Services.Implement
{
    public class RateLimitingService : IRateLimitingService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IAuditService _auditService;
        private readonly IDistributedCache _cache;
        private readonly ILogger<RateLimitingService> _logger;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;

        // In-memory cache for frequently accessed rules
        private static readonly ConcurrentDictionary<string, RateLimitRule> _ruleCache = new();
        private static DateTime _lastRuleCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan RuleCacheExpiry = TimeSpan.FromMinutes(5);

        // Default rate limit rules
        private static readonly Dictionary<string, RateLimitRule> DefaultRules = new()
        {
            {
                "send_message_standard",
                new RateLimitRule
                {
                    Name = "Standard User Message Rate Limit",
                    Action = "send_message",
                    UserType = "standard",
                    MaxRequests = 60, // 60 messages per hour
                    TimeWindow = TimeSpan.FromHours(1),
                    WindowType = "sliding",
                    IsActive = true,
                    Priority = 100,
                    Description = "Standard rate limit for sending messages"
                }
            },
            {
                "send_message_premium",
                new RateLimitRule
                {
                    Name = "Premium User Message Rate Limit",
                    Action = "send_message",
                    UserType = "premium",
                    MaxRequests = 200, // 200 messages per hour
                    TimeWindow = TimeSpan.FromHours(1),
                    WindowType = "sliding",
                    IsActive = true,
                    Priority = 90,
                    Description = "Premium rate limit for sending messages"
                }
            },
            {
                "start_streaming_standard",
                new RateLimitRule
                {
                    Name = "Standard Streaming Rate Limit",
                    Action = "start_streaming",
                    UserType = "standard",
                    MaxRequests = 20, // 20 streams per hour
                    TimeWindow = TimeSpan.FromHours(1),
                    WindowType = "sliding",
                    IsActive = true,
                    Priority = 100,
                    Description = "Standard rate limit for streaming requests"
                }
            },
            {
                "api_calls_burst",
                new RateLimitRule
                {
                    Name = "API Burst Protection",
                    Action = "*",
                    UserType = "all",
                    MaxRequests = 10, // 10 requests per minute (burst protection)
                    TimeWindow = TimeSpan.FromMinutes(1),
                    WindowType = "sliding",
                    IsActive = true,
                    Priority = 10,
                    Description = "Burst protection for all API calls"
                }
            }
        };

        public RateLimitingService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IAuditService auditService,
            IDistributedCache cache,
            ILogger<RateLimitingService> logger,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _auditService = auditService;
            _cache = cache;
            _logger = logger;
            _configuration = configuration;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task RecordRequestAsync(Guid userId, string action)
        {
            try
            {
                _logger.LogDebug("Recording request for user {UserId}, action: {Action}", userId, action);

                // Get applicable rate limit rules
                var rules = await GetApplicableRulesAsync(userId, action);

                foreach (var rule in rules)
                {
                    await RecordRequestForRuleAsync(userId, action, rule);
                }

                // Update global request statistics
                await UpdateGlobalStatsAsync(userId, action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording request for user {UserId}, action: {Action}", userId, action);

                // Rate limiting failures should not break the main flow
                // But we should monitor these failures
                await _auditService.LogSecurityEventAsync(userId, "RateLimitRecordFailure",
                    $"Failed to record rate limit for action {action}: {ex.Message}", "medium");
            }
        }

        public async Task<bool> IsWithinLimitAsync(Guid userId, string action)
        {
            try
            {
                _logger.LogDebug("Checking rate limit for user {UserId}, action: {Action}", userId, action);

                // Check if user is globally blocked
                if (await IsUserGloballyBlockedAsync(userId))
                {
                    _logger.LogWarning("User {UserId} is globally blocked", userId);
                    return false;
                }

                // Get applicable rate limit rules
                var rules = await GetApplicableRulesAsync(userId, action);

                foreach (var rule in rules.OrderBy(r => r.Priority))
                {
                    var result = await CheckRateLimitAsync(userId, action, rule);

                    if (!result.IsAllowed)
                    {
                        _logger.LogWarning("Rate limit exceeded for user {UserId}, action: {Action}, rule: {RuleName}",
                            userId, action, rule.Name);

                        // Log violation
                        await LogRateLimitViolationAsync(userId, action, rule, result);

                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rate limit for user {UserId}, action: {Action}", userId, action);

                // On error, we should be conservative and allow the request
                // But log this as a potential security issue
                await _auditService.LogSecurityEventAsync(userId, "RateLimitCheckFailure",
                    $"Failed to check rate limit for action {action}: {ex.Message}", "high");

                return true; // Fail open - allow request when rate limiting fails
            }
        }

        public async Task ResetLimitsAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Resetting rate limits for user {UserId}", userId);

                // Reset in-memory cache
                var cacheKeys = await GetUserCacheKeysAsync(userId);
                foreach (var key in cacheKeys)
                {
                    await _cache.RemoveAsync(key);
                }

                // Reset database status
                var statusRepo = _unitOfWork.GetRepository<UserRateLimitStatus>();
                var userStatuses = await statusRepo.GetListAsync(predicate: s => s.UserId == userId);

                foreach (var status in userStatuses)
                {
                    status.CurrentCount = 0;
                    status.WindowStart = DateTime.UtcNow;
                    status.WindowEnd = DateTime.UtcNow.Add(TimeSpan.FromHours(1)); // Default window
                    status.IsBlocked = false;
                    status.BlockedUntil = null;
                    status.BlockReason = null;
                    status.UpdatedAt = DateTime.UtcNow;

                    statusRepo.UpdateAsync(status);
                }

                await _unitOfWork.CommitAsync();

                // Log the reset operation
                await _auditService.LogAsync(null, "ResetRateLimits", "UserRateLimitStatus", userId.ToString(),
                    null, new { UserId = userId, ResetAt = DateTime.UtcNow });

                _logger.LogInformation("Rate limits reset successfully for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting rate limits for user {UserId}", userId);
                throw;
            }
        }

        // Additional methods for enhanced functionality
        public async Task<RateLimitStats> GetUserRateLimitStatsAsync(Guid userId)
        {
            try
            {
                var stats = new RateLimitStats
                {
                    UserId = userId,
                    GeneratedAt = DateTime.UtcNow
                };

                // Get user's current rate limit statuses
                var statusRepo = _unitOfWork.GetRepository<UserRateLimitStatus>();
                var userStatuses = await statusRepo.GetListAsync(predicate: s => s.UserId == userId);

                foreach (var status in userStatuses)
                {
                    var actionStats = new ActionStats
                    {
                        Action = status.Action,
                        CurrentCount = status.CurrentCount,
                        MaxAllowed = await GetMaxAllowedForActionAsync(userId, status.Action),
                        TimeWindow = status.WindowEnd - status.WindowStart,
                        WindowStart = status.WindowStart,
                        WindowEnd = status.WindowEnd,
                        ResetTime = status.WindowEnd - DateTime.UtcNow
                    };

                    actionStats.RemainingRequests = Math.Max(0, actionStats.MaxAllowed - actionStats.CurrentCount);
                    stats.ActionStats[status.Action] = actionStats;
                }

                // Get violation statistics
                var violationRepo = _unitOfWork.GetRepository<RateLimitViolation>();
                var violations = await violationRepo.GetListAsync(predicate: v => v.UserId == userId);

                stats.TotalViolations = violations.Count;
                stats.LastViolation = violations.OrderByDescending(v => v.ViolationTime).FirstOrDefault()?.ViolationTime;

                // Check if currently blocked
                var currentStatus = userStatuses.FirstOrDefault(s => s.IsBlocked && s.BlockedUntil > DateTime.UtcNow);
                if (currentStatus != null)
                {
                    stats.IsCurrentlyBlocked = true;
                    stats.BlockedUntil = currentStatus.BlockedUntil;
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rate limit stats for user {UserId}", userId);

                return new RateLimitStats
                {
                    UserId = userId,
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<bool> TemporarilyBlockUserAsync(Guid userId, TimeSpan duration, string reason)
        {
            try
            {
                _logger.LogInformation("Temporarily blocking user {UserId} for {Duration}, reason: {Reason}",
                    userId, duration, reason);

                var statusRepo = _unitOfWork.GetRepository<UserRateLimitStatus>();
                var userStatuses = await statusRepo.GetListAsync(predicate: s => s.UserId == userId);

                var blockedUntil = DateTime.UtcNow.Add(duration);

                foreach (var status in userStatuses)
                {
                    status.IsBlocked = true;
                    status.BlockedUntil = blockedUntil;
                    status.BlockReason = reason;
                    status.UpdatedAt = DateTime.UtcNow;

                    statusRepo.UpdateAsync(status);
                }

                // If no existing statuses, create a global block status
                if (!userStatuses.Any())
                {
                    var blockStatus = new UserRateLimitStatus
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Action = "*",
                        CurrentCount = 0,
                        WindowStart = DateTime.UtcNow,
                        WindowEnd = DateTime.UtcNow.AddHours(1),
                        LastRequestTime = DateTime.UtcNow,
                        IsBlocked = true,
                        BlockedUntil = blockedUntil,
                        BlockReason = reason,
                        ViolationCount = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await statusRepo.InsertAsync(blockStatus);
                }

                await _unitOfWork.CommitAsync();

                // Log the blocking action
                await _auditService.LogSecurityEventAsync(null, "UserTemporarilyBlocked",
                    $"User {userId} temporarily blocked for {duration}. Reason: {reason}", "high");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error temporarily blocking user {UserId}", userId);
                return false;
            }
        }

        // Private helper methods
        private async Task<List<RateLimitRule>> GetApplicableRulesAsync(Guid userId, string action)
        {
            var rules = new List<RateLimitRule>();

            try
            {
                // Refresh rule cache if needed
                await RefreshRuleCacheIfNeededAsync();

                // Get user type (this would typically come from user service or auth context)
                var userType = await GetUserTypeAsync(userId);

                // Find applicable rules from cache
                var applicableRules = _ruleCache.Values.Where(rule =>
                    rule.IsActive &&
                    (rule.Action == action || rule.Action == "*") &&
                    (rule.UserType == userType || rule.UserType == "all"))
                    .OrderBy(r => r.Priority)
                    .ToList();

                rules.AddRange(applicableRules);

                // If no custom rules found, use defaults
                if (!rules.Any())
                {
                    var defaultRuleKey = $"{action}_{userType}";
                    if (DefaultRules.TryGetValue(defaultRuleKey, out var defaultRule))
                    {
                        rules.Add(defaultRule);
                    }
                    else if (DefaultRules.TryGetValue("api_calls_burst", out var burstRule))
                    {
                        rules.Add(burstRule);
                    }
                }

                return rules;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting applicable rules, using defaults");

                // Return burst protection rule as fallback
                return new List<RateLimitRule> { DefaultRules["api_calls_burst"] };
            }
        }

        private async Task RefreshRuleCacheIfNeededAsync()
        {
            if (DateTime.UtcNow - _lastRuleCacheUpdate < RuleCacheExpiry)
                return;

            try
            {
                var ruleRepo = _unitOfWork.GetRepository<RateLimitRule>();
                var dbRules = await ruleRepo.GetListAsync(predicate: r => r.IsActive);

                _ruleCache.Clear();
                foreach (var rule in dbRules)
                {
                    var cacheKey = $"{rule.Action}_{rule.UserType}_{rule.Priority}";
                    _ruleCache.TryAdd(cacheKey, rule);
                }

                _lastRuleCacheUpdate = DateTime.UtcNow;

                _logger.LogDebug("Rate limit rule cache refreshed with {RuleCount} rules", dbRules.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error refreshing rule cache");
            }
        }

        private async Task<string> GetUserTypeAsync(Guid userId)
        {
            try
            {
                // This would typically integrate with user service or auth context
                // For now, return "standard" as default
                // In real implementation:
                // - Check user's subscription/plan
                // - Check user's role/permissions
                // - Return appropriate user type

                return "standard";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting user type for {UserId}, defaulting to standard", userId);
                return "standard";
            }
        }

        private async Task RecordRequestForRuleAsync(Guid userId, string action, RateLimitRule rule)
        {
            var cacheKey = $"rate_limit:{userId}:{action}:{rule.Name}";
            var lockKey = $"{cacheKey}:lock";

            try
            {
                // Simple distributed locking using cache
                var lockAcquired = await TryAcquireLockAsync(lockKey, TimeSpan.FromSeconds(5));
                if (!lockAcquired)
                {
                    _logger.LogWarning("Could not acquire lock for rate limiting user {UserId}, action {Action}", userId, action);
                    return;
                }

                // Get current status from cache
                var statusJson = await _cache.GetStringAsync(cacheKey);
                UserRateLimitStatus status;

                if (!string.IsNullOrEmpty(statusJson))
                {
                    status = JsonSerializer.Deserialize<UserRateLimitStatus>(statusJson, _jsonOptions);

                    // Check if window has expired
                    if (DateTime.UtcNow > status.WindowEnd)
                    {
                        // Reset window
                        status.WindowStart = DateTime.UtcNow;
                        status.WindowEnd = DateTime.UtcNow.Add(rule.TimeWindow);
                        status.CurrentCount = 0;
                    }
                }
                else
                {
                    // Create new status
                    status = new UserRateLimitStatus
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Action = action,
                        CurrentCount = 0,
                        WindowStart = DateTime.UtcNow,
                        WindowEnd = DateTime.UtcNow.Add(rule.TimeWindow),
                        IsBlocked = false,
                        ViolationCount = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                }

                // Increment count
                status.CurrentCount++;
                status.LastRequestTime = DateTime.UtcNow;
                status.UpdatedAt = DateTime.UtcNow;

                // Update cache
                var updatedJson = JsonSerializer.Serialize(status, _jsonOptions);
                await _cache.SetStringAsync(cacheKey, updatedJson, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = rule.TimeWindow.Add(TimeSpan.FromMinutes(5))
                });

                // Periodically persist to database
                if (status.CurrentCount % 10 == 0) // Every 10 requests
                {
                    await PersistStatusToDatabaseAsync(status);
                }
            }
            finally
            {
                await ReleaseLockAsync(lockKey);
            }
        }

        private async Task<RateLimitResult> CheckRateLimitAsync(Guid userId, string action, RateLimitRule rule)
        {
            var cacheKey = $"rate_limit:{userId}:{action}:{rule.Name}";

            try
            {
                var statusJson = await _cache.GetStringAsync(cacheKey);
                UserRateLimitStatus status = null;

                if (!string.IsNullOrEmpty(statusJson))
                {
                    status = JsonSerializer.Deserialize<UserRateLimitStatus>(statusJson, _jsonOptions);
                }

                // If no status in cache, check database
                if (status == null)
                {
                    status = await GetStatusFromDatabaseAsync(userId, action);
                }

                // If still no status, user is within limits
                if (status == null)
                {
                    return new RateLimitResult
                    {
                        IsAllowed = true,
                        RemainingRequests = rule.MaxRequests,
                        ResetTime = rule.TimeWindow,
                        LimitInfo = new RateLimitInfo
                        {
                            Action = action,
                            MaxRequests = rule.MaxRequests,
                            TimeWindow = rule.TimeWindow,
                            CurrentCount = 0,
                            WindowStart = DateTime.UtcNow,
                            WindowEnd = DateTime.UtcNow.Add(rule.TimeWindow),
                            RuleName = rule.Name,
                            WindowType = rule.WindowType
                        }
                    };
                }

                // Check if currently blocked
                if (status.IsBlocked && status.BlockedUntil > DateTime.UtcNow)
                {
                    return new RateLimitResult
                    {
                        IsAllowed = false,
                        Reason = $"User temporarily blocked: {status.BlockReason}",
                        RemainingRequests = 0,
                        RetryAfter = status.BlockedUntil,
                        ResetTime = status.BlockedUntil.Value - DateTime.UtcNow,
                        LimitInfo = CreateLimitInfo(status, rule)
                    };
                }

                // Check if window has expired
                if (DateTime.UtcNow > status.WindowEnd)
                {
                    // Window expired, user is within limits
                    return new RateLimitResult
                    {
                        IsAllowed = true,
                        RemainingRequests = rule.MaxRequests,
                        ResetTime = rule.TimeWindow,
                        LimitInfo = CreateLimitInfo(status, rule)
                    };
                }

                // Check if within limit
                var isWithinLimit = status.CurrentCount < rule.MaxRequests;
                var remainingRequests = Math.Max(0, rule.MaxRequests - status.CurrentCount);

                return new RateLimitResult
                {
                    IsAllowed = isWithinLimit,
                    Reason = isWithinLimit ? null : $"Rate limit exceeded: {status.CurrentCount}/{rule.MaxRequests} requests in {rule.TimeWindow}",
                    RemainingRequests = remainingRequests,
                    ResetTime = status.WindowEnd - DateTime.UtcNow,
                    RetryAfter = isWithinLimit ? null : status.WindowEnd,
                    LimitInfo = CreateLimitInfo(status, rule)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rate limit for user {UserId}, action {Action}", userId, action);

                // On error, allow the request but log the issue
                return new RateLimitResult
                {
                    IsAllowed = true,
                    Reason = "Rate limit check failed - allowing request",
                    RemainingRequests = rule.MaxRequests,
                    ResetTime = rule.TimeWindow,
                    Metadata = new Dictionary<string, object> { { "Error", ex.Message } }
                };
            }
        }

        private async Task<bool> IsUserGloballyBlockedAsync(Guid userId)
        {
            try
            {
                var cacheKey = $"global_block:{userId}";
                var blockedJson = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(blockedJson))
                {
                    var blockInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(blockedJson, _jsonOptions);
                    if (blockInfo.TryGetValue("blockedUntil", out var blockedUntilObj))
                    {
                        if (DateTime.TryParse(blockedUntilObj.ToString(), out var blockedUntil))
                        {
                            return DateTime.UtcNow < blockedUntil;
                        }
                    }
                }

                // Check database as fallback
                var statusRepo = _unitOfWork.GetRepository<UserRateLimitStatus>();
                var globalBlock = await statusRepo.SingleOrDefaultAsync(predicate:
                    s => s.UserId == userId && s.Action == "*" && s.IsBlocked && s.BlockedUntil > DateTime.UtcNow);

                return globalBlock != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking global block status for user {UserId}", userId);
                return false;
            }
        }

        private async Task LogRateLimitViolationAsync(Guid userId, string action, RateLimitRule rule, RateLimitResult result)
        {
            try
            {
                var violationRepo = _unitOfWork.GetRepository<RateLimitViolation>();

                var violation = new RateLimitViolation
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = action,
                    RuleName = rule.Name,
                    RequestCount = result.LimitInfo?.CurrentCount ?? 0,
                    MaxAllowed = rule.MaxRequests,
                    TimeWindow = rule.TimeWindow,
                    ViolationTime = DateTime.UtcNow,
                    Severity = DetermineViolationSeverity(result.LimitInfo?.CurrentCount ?? 0, rule.MaxRequests),
                    IsResolved = false,
                    Metadata = new Dictionary<string, object>
                    {
                        { "RuleName", rule.Name },
                        { "WindowType", rule.WindowType },
                        { "UserType", await GetUserTypeAsync(userId) }
                    }
                };

                await violationRepo.InsertAsync(violation);
                await _unitOfWork.CommitAsync();

                // Log to audit service
                await _auditService.LogSecurityEventAsync(userId, "RateLimitViolation",
                    $"Rate limit exceeded for action {action}: {violation.RequestCount}/{rule.MaxRequests} requests",
                    violation.Severity);

                // Auto-block for severe violations
                if (violation.Severity == "critical")
                {
                    await TemporarilyBlockUserAsync(userId, TimeSpan.FromHours(1), "Automatic block due to severe rate limit violation");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging rate limit violation for user {UserId}", userId);
            }
        }

        private async Task UpdateGlobalStatsAsync(Guid userId, string action)
        {
            try
            {
                var statsKey = $"global_stats:{DateTime.UtcNow:yyyy-MM-dd}";
                var userStatsKey = $"user_stats:{userId}:{DateTime.UtcNow:yyyy-MM-dd}";

                // Update global daily stats
                await IncrementCounterAsync(statsKey, $"total_requests");
                await IncrementCounterAsync(statsKey, $"action_{action}");

                // Update user daily stats
                await IncrementCounterAsync(userStatsKey, $"total_requests");
                await IncrementCounterAsync(userStatsKey, $"action_{action}");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error updating global stats - non-critical");
            }
        }

        private async Task<bool> TryAcquireLockAsync(string lockKey, TimeSpan timeout)
        {
            try
            {
                var lockValue = Guid.NewGuid().ToString();
                var acquired = await _cache.GetStringAsync(lockKey) == null;

                if (acquired)
                {
                    await _cache.SetStringAsync(lockKey, lockValue, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = timeout
                    });
                }

                return acquired;
            }
            catch
            {
                return false;
            }
        }

        private async Task ReleaseLockAsync(string lockKey)
        {
            try
            {
                await _cache.RemoveAsync(lockKey);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error releasing lock {LockKey}", lockKey);
            }
        }

        private async Task PersistStatusToDatabaseAsync(UserRateLimitStatus status)
        {
            try
            {
                var statusRepo = _unitOfWork.GetRepository<UserRateLimitStatus>();
                var existingStatus = await statusRepo.SingleOrDefaultAsync(predicate:
                    s => s.UserId == status.UserId && s.Action == status.Action);

                if (existingStatus != null)
                {
                    existingStatus.CurrentCount = status.CurrentCount;
                    existingStatus.WindowStart = status.WindowStart;
                    existingStatus.WindowEnd = status.WindowEnd;
                    existingStatus.LastRequestTime = status.LastRequestTime;
                    existingStatus.UpdatedAt = DateTime.UtcNow;

                    statusRepo.UpdateAsync(existingStatus);
                }
                else
                {
                    await statusRepo.InsertAsync(status);
                }

                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error persisting status to database - non-critical");
            }
        }

        private async Task<UserRateLimitStatus> GetStatusFromDatabaseAsync(Guid userId, string action)
        {
            try
            {
                var statusRepo = _unitOfWork.GetRepository<UserRateLimitStatus>();
                return await statusRepo.SingleOrDefaultAsync(predicate:
                    s => s.UserId == userId && s.Action == action);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting status from database");
                return null;
            }
        }

        private RateLimitInfo CreateLimitInfo(UserRateLimitStatus status, RateLimitRule rule)
        {
            return new RateLimitInfo
            {
                Action = status.Action,
                MaxRequests = rule.MaxRequests,
                TimeWindow = rule.TimeWindow,
                CurrentCount = status.CurrentCount,
                WindowStart = status.WindowStart,
                WindowEnd = status.WindowEnd,
                RuleName = rule.Name,
                WindowType = rule.WindowType
            };
        }

        private async Task<int> GetMaxAllowedForActionAsync(Guid userId, string action)
        {
            try
            {
                var rules = await GetApplicableRulesAsync(userId, action);
                return rules.FirstOrDefault()?.MaxRequests ?? 60; // Default
            }
            catch
            {
                return 60; // Safe default
            }
        }

        private async Task<List<string>> GetUserCacheKeysAsync(Guid userId)
        {
            try
            {
                // In a real implementation, you might keep track of cache keys
                // For now, return common patterns
                var actions = new[] { "send_message", "start_streaming", "get_history", "update_preferences" };
                var keys = new List<string>();

                foreach (var action in actions)
                {
                    keys.Add($"rate_limit:{userId}:{action}:*");
                }

                keys.Add($"global_block:{userId}");
                keys.Add($"user_stats:{userId}:*");

                return keys;
            }
            catch
            {
                return new List<string>();
            }
        }

        private string DetermineViolationSeverity(int currentCount, int maxAllowed)
        {
            var exceedPercentage = (double)currentCount / maxAllowed;

            return exceedPercentage switch
            {
                >= 3.0 => "critical",  // 300% over limit
                >= 2.0 => "high",      // 200% over limit
                >= 1.5 => "medium",    // 150% over limit
                _ => "low"             // Just over limit
            };
        }

        private async Task IncrementCounterAsync(string key, string field)
        {
            try
            {
                var counterKey = $"{key}:{field}";
                var currentValue = await _cache.GetStringAsync(counterKey);
                var count = string.IsNullOrEmpty(currentValue) ? 0 : int.Parse(currentValue);

                await _cache.SetStringAsync(counterKey, (count + 1).ToString(), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) // Keep stats for a week
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error incrementing counter {Key}:{Field}", key, field);
            }
        }
    }

    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public string Reason { get; set; }
        public int RemainingRequests { get; set; }
        public TimeSpan ResetTime { get; set; }
        public DateTime? RetryAfter { get; set; }
        public RateLimitInfo LimitInfo { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class RateLimitInfo
    {
        public string Action { get; set; }
        public int MaxRequests { get; set; }
        public TimeSpan TimeWindow { get; set; }
        public int CurrentCount { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public string RuleName { get; set; }
        public string WindowType { get; set; }
    }

    public class RateLimitStats
    {
        public Guid UserId { get; set; }
        public Dictionary<string, ActionStats> ActionStats { get; set; } = new();
        public int TotalViolations { get; set; }
        public DateTime? LastViolation { get; set; }
        public bool IsCurrentlyBlocked { get; set; }
        public DateTime? BlockedUntil { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class ActionStats
    {
        public string Action { get; set; }
        public int CurrentCount { get; set; }
        public int MaxAllowed { get; set; }
        public int RemainingRequests { get; set; }
        public TimeSpan TimeWindow { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public TimeSpan ResetTime { get; set; }
    }


}