using AutoMapper;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChatBox.API.Services.Implement
{
    public class AuditService : IAuditService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly ILogger<AuditService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;
        public AuditService(
             IUnitOfWork<ChatBoxDbContext> unitOfWork,
             ILogger<AuditService> logger,
             IConfiguration configuration,
             IHttpContextAccessor httpContextAccessor,
             IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _mapper = mapper;
        }

        public async Task LogAsync(Guid? userId, string action, string entityType, string entityId,
                  object oldValues = null, object newValues = null, string ipAddress = null, string userAgent = null)
        {
            try
            {
                var auditLogRepo = _unitOfWork.GetRepository<AuditLog>();
                var retentionDays = _configuration.GetValue<int>("AuditService:DefaultRetentionDays", 365);

                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    IpAddress = ipAddress ?? GetClientIpAddress(),
                    UserAgent = userAgent ?? GetUserAgent(),
                    Timestamp = DateTime.UtcNow,
                    SessionId = GenerateSessionId(),
                    Source = DetermineSource(userAgent),
                    Category = DetermineCategory(action, entityType),
                    Severity = DetermineSeverity(action, entityType),
                    IsDeleted = false,
                    RetentionDate = DateTime.UtcNow.AddDays(retentionDays),
                    OldValues = oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
                    NewValues = newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null
                };

                await auditLogRepo.InsertAsync(auditLog);
                await _unitOfWork.CommitAsync();

                _logger.LogDebug("Audit log created: {AuditId} for {EntityType}:{EntityId} by user {UserId}",
                    auditLog.Id, entityType, entityId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audit log for {EntityType}:{EntityId} by user {UserId}",
                    entityType, entityId, userId);
            }
        }

        public async Task LogSecurityEventAsync(Guid? userId, string eventType, string description,
            string severity = "medium", string ipAddress = null, Dictionary<string, object> metadata = null)
        {
            try
            {
                var securityAuditRepo = _unitOfWork.GetRepository<SecurityAuditLog>();
                var retentionDays = _configuration.GetValue<int>("AuditService:SecurityEventRetentionDays", 2555);

                var securityLog = new SecurityAuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EventType = eventType,
                    Description = description,
                    Severity = severity,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = ipAddress ?? GetClientIpAddress(),
                    Source = _configuration.GetValue<string>("AuditService:DefaultSource", "system"),
                    ThreatLevel = DetermineThreatLevel(eventType, severity),
                    RequiresInvestigation = RequiresInvestigation(severity, eventType),
                    InvestigationStatus = _configuration.GetValue<string>("AuditService:DefaultInvestigationStatus", "new"),
                    IsArchived = false,
                };

                await securityAuditRepo.InsertAsync(securityLog);
                await _unitOfWork.CommitAsync();

                _logger.LogWarning("Security event logged: {EventType} for user {UserId} with severity {Severity}",
                    eventType, userId, severity);

                if (severity == "high" || severity == "critical")
                {
                    await TriggerSecurityAlertAsync(securityLog);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging security event {EventType} for user {UserId}", eventType, userId);
            }
        }

        public async Task<List<AuditLog>> GetUserAuditLogsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null, int limit = 100)
        {
            try
            {
                var auditLogRepo = _unitOfWork.GetRepository<AuditLog>();
                var maxLimit = _configuration.GetValue<int>("AuditService:MaxQueryLimit", 1000);
                limit = Math.Min(limit, maxLimit);

                var predicate = BuildUserAuditPredicate(userId, fromDate, toDate);
                var auditLogs = await auditLogRepo.GetListAsync(
                    predicate: predicate,
                    orderBy: logs => logs.OrderByDescending(l => l.Timestamp));

                return auditLogs.Take(limit).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs for user {UserId}", userId);
                return new List<AuditLog>();
            }
        }
        public async Task<List<AuditLog>> GetSystemAuditLogsAsync(DateTime? fromDate = null, DateTime? toDate = null, int limit = 1000)
        {
            try
            {
                var auditLogRepo = _unitOfWork.GetRepository<AuditLog>();
                var maxLimit = _configuration.GetValue<int>("AuditService:MaxSystemQueryLimit", 5000);
                limit = Math.Min(limit, maxLimit);

                var predicate = BuildSystemAuditPredicate(fromDate, toDate);
                var auditLogs = await auditLogRepo.GetListAsync(
                    predicate: predicate,
                    orderBy: logs => logs.OrderByDescending(l => l.Timestamp));

                return auditLogs.Take(limit).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system audit logs");
                return new List<AuditLog>();
            }
        }

        public async Task<List<AuditLog>> SearchAuditLogsAsync(string searchTerm, DateTime? fromDate = null, DateTime? toDate = null, int limit = 100)
        {
            try
            {
                var auditLogRepo = _unitOfWork.GetRepository<AuditLog>();
                var maxLimit = _configuration.GetValue<int>("AuditService:MaxSearchLimit", 500);
                limit = Math.Min(limit, maxLimit);

                var predicate = BuildSearchAuditPredicate(searchTerm, fromDate, toDate);
                var auditLogs = await auditLogRepo.GetListAsync(
                    predicate: predicate,
                    orderBy: logs => logs.OrderByDescending(l => l.Timestamp));

                return auditLogs.Take(limit).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching audit logs with term: {SearchTerm}", searchTerm);
                return new List<AuditLog>();
            }
        }

        public async Task CleanupOldLogsAsync(int retentionDays = 90)
        {
            try
            {
                var batchSize = _configuration.GetValue<int>("AuditService:CleanupBatchSize", 1000);
                _logger.LogInformation("Starting audit log cleanup for logs older than {RetentionDays} days", retentionDays);

                var auditLogRepo = _unitOfWork.GetRepository<AuditLog>();
                var securityAuditRepo = _unitOfWork.GetRepository<SecurityAuditLog>();

                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                // Process in batches for better performance
                var expiredAuditLogs = await auditLogRepo.GetListAsync(
                    predicate: log => log.RetentionDate.HasValue && log.RetentionDate.Value < DateTime.UtcNow && !log.IsDeleted);

                var expiredSecurityLogs = await securityAuditRepo.GetListAsync(
                    predicate: log => log.Timestamp < cutoffDate && !log.IsArchived);

                // Archive security logs
                foreach (var batch in expiredSecurityLogs.Chunk(batchSize))
                {
                    foreach (var securityLog in batch)
                    {
                        securityLog.IsArchived = true;
                        securityLog.ArchiveDate = DateTime.UtcNow;
                        securityAuditRepo.UpdateAsync(securityLog);
                    }
                    await _unitOfWork.CommitAsync();
                }

                // Soft delete audit logs
                foreach (var batch in expiredAuditLogs.Chunk(batchSize))
                {
                    foreach (var auditLog in batch)
                    {
                        auditLog.IsDeleted = true;
                        auditLogRepo.UpdateAsync(auditLog);
                    }
                    await _unitOfWork.CommitAsync();
                }

                _logger.LogInformation("Audit log cleanup completed. Archived {SecurityLogCount} security logs, soft-deleted {AuditLogCount} audit logs",
                    expiredSecurityLogs.Count, expiredAuditLogs.Count);

                await LogAsync(null, "CleanupOldLogs", "AuditService", "bulk_cleanup",
                    null, new { RetentionDays = retentionDays, ArchivedSecurityLogs = expiredSecurityLogs.Count, DeletedAuditLogs = expiredAuditLogs.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during audit log cleanup");
            }
        }
        // Private helper methods
        private string GetClientIpAddress()
        {
            var context = _httpContextAccessor.HttpContext;
            return context?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private string GetUserAgent()
        {
            var context = _httpContextAccessor.HttpContext;
            return context?.Request?.Headers["User-Agent"].ToString() ?? "unknown";
        }

        private string GenerateSessionId()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            return session?.Id ?? Guid.NewGuid().ToString("N")[..16];
        }

        private string DetermineSource(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return _configuration.GetValue<string>("AuditService:DefaultSource", "unknown");

            var sourceMap = _configuration.GetSection("AuditService:SourceMapping").Get<Dictionary<string, string>>() ?? new();

            foreach (var mapping in sourceMap)
            {
                if (userAgent.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
                    return mapping.Value;
            }

            return userAgent.Contains("Mobile") ? "mobile" :
                   userAgent.Contains("API") ? "api" : "web";
        }

        private string DetermineCategory(string action, string entityType)
        {
            var categories = _configuration.GetSection("AuditService:CategoryMapping").Get<Dictionary<string, string>>() ?? new();

            var key = $"{action}_{entityType}";
            if (categories.TryGetValue(key, out var category))
                return category;

            var securityActions = _configuration.GetSection("AuditService:SecurityActions").Get<string[]>() ??
                new[] { "Login", "Logout", "PasswordChange", "SecurityEvent", "AccessDenied" };

            return securityActions.Contains(action, StringComparer.OrdinalIgnoreCase) ? "security_event" : "user_action";
        }

        private string DetermineSeverity(string action, string entityType)
        {
            var severityMap = _configuration.GetSection("AuditService:SeverityMapping").Get<Dictionary<string, string>>() ?? new();

            var key = $"{action}_{entityType}";
            if (severityMap.TryGetValue(key, out var severity))
                return severity;

            var highSeverityActions = _configuration.GetSection("AuditService:HighSeverityActions").Get<string[]>() ??
                new[] { "Delete", "SecurityEvent", "AccessDenied", "PasswordChange" };

            return highSeverityActions.Contains(action, StringComparer.OrdinalIgnoreCase) ? "high" : "medium";
        }

        private string DetermineThreatLevel(string eventType, string severity)
        {
            var threatMap = _configuration.GetSection("AuditService:ThreatLevelMapping").Get<Dictionary<string, string>>() ?? new();

            if (threatMap.TryGetValue(eventType, out var threatLevel))
                return threatLevel;

            return severity switch
            {
                "critical" => "critical",
                "high" => "high",
                "medium" => "medium",
                _ => "low"
            };
        }

        private bool RequiresInvestigation(string severity, string eventType)
        {
            var investigationRequired = _configuration.GetSection("AuditService:InvestigationRequired").Get<string[]>() ??
                new[] { "critical", "high" };

            var autoInvestigateEvents = _configuration.GetSection("AuditService:AutoInvestigateEvents").Get<string[]>() ??
                new[] { "DataBreach", "SystemCompromise", "SecurityViolation" };

            return investigationRequired.Contains(severity, StringComparer.OrdinalIgnoreCase) ||
                   autoInvestigateEvents.Contains(eventType, StringComparer.OrdinalIgnoreCase);
        }

        private async Task TriggerSecurityAlertAsync(SecurityAuditLog securityLog)
        {
            try
            {
                var alertConfig = _configuration.GetSection("AuditService:AlertConfiguration");
                var enableEmailAlerts = alertConfig.GetValue<bool>("EnableEmailAlerts", true);
                var enableLogAlerts = alertConfig.GetValue<bool>("EnableLogAlerts", true);

                if (enableLogAlerts)
                {
                    _logger.LogCritical("SECURITY ALERT: {EventType} for user {UserId} - {Description}",
                        securityLog.EventType, securityLog.UserId, securityLog.Description);
                }

                // Additional alert mechanisms can be implemented here
                // Email, Slack, SMS, etc. based on configuration
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering security alert for {SecurityLogId}", securityLog.Id);
            }
        }

        private System.Linq.Expressions.Expression<Func<AuditLog, bool>> BuildUserAuditPredicate(
            Guid userId, DateTime? fromDate, DateTime? toDate)
        {
            return log => log.UserId == userId &&
                         !log.IsDeleted &&
                         (!fromDate.HasValue || log.Timestamp >= fromDate.Value) &&
                         (!toDate.HasValue || log.Timestamp <= toDate.Value);
        }

        private System.Linq.Expressions.Expression<Func<AuditLog, bool>> BuildSystemAuditPredicate(
            DateTime? fromDate, DateTime? toDate)
        {
            var systemCategories = _configuration.GetSection("AuditService:SystemCategories").Get<string[]>() ??
                new[] { "system_event" };

            return log => !log.IsDeleted &&
                         systemCategories.Contains(log.Category) &&
                         (!fromDate.HasValue || log.Timestamp >= fromDate.Value) &&
                         (!toDate.HasValue || log.Timestamp <= toDate.Value);
        }

        private System.Linq.Expressions.Expression<Func<AuditLog, bool>> BuildSearchAuditPredicate(
            string searchTerm, DateTime? fromDate, DateTime? toDate)
        {
            var searchLower = searchTerm.ToLower();
            return log => !log.IsDeleted &&
                         (log.Action.ToLower().Contains(searchLower) ||
                          log.EntityType.ToLower().Contains(searchLower) ||
                          log.EntityId.ToLower().Contains(searchLower) ||
                          (log.OldValues != null && log.OldValues.ToLower().Contains(searchLower)) ||
                          (log.NewValues != null && log.NewValues.ToLower().Contains(searchLower))) &&
                         (!fromDate.HasValue || log.Timestamp >= fromDate.Value) &&
                         (!toDate.HasValue || log.Timestamp <= toDate.Value);
        }
    }
}
