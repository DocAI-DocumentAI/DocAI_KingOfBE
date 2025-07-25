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
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Default audit configurations
        private static readonly Dictionary<string, AuditConfiguration> DefaultConfigurations = new()
        {
            {
                "ChatMessage",
                new AuditConfiguration
                {
                    EntityType = "ChatMessage",
                    ActionType = "*",
                    IsEnabled = true,
                    RetentionDays = 2555, // 7 years for compliance
                    LogLevel = "standard",
                    IncludeOldValues = true,
                    IncludeNewValues = true,
                    RequireApproval = false,
                    SensitiveFields = new List<string> { "Content", "AiResponse" }
                }
            },
            {
                "UserPreference",
                new AuditConfiguration
                {
                    EntityType = "UserPreference",
                    ActionType = "*",
                    IsEnabled = true,
                    RetentionDays = 1095, // 3 years
                    LogLevel = "standard",
                    IncludeOldValues = true,
                    IncludeNewValues = true,
                    RequireApproval = false,
                    SensitiveFields = new List<string>()
                }
            },
            {
                "SecurityEvent",
                new AuditConfiguration
                {
                    EntityType = "SecurityEvent",
                    ActionType = "*",
                    IsEnabled = true,
                    RetentionDays = 3650, // 10 years for security events
                    LogLevel = "full",
                    IncludeOldValues = true,
                    IncludeNewValues = true,
                    RequireApproval = false,
                    SensitiveFields = new List<string> { "IpAddress", "UserAgent" }
                }
            }
        };

        public AuditService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            ILogger<AuditService> logger,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(Guid? userId, string action, string entityType, string entityId,
            object oldValues = null, object newValues = null, string ipAddress = null, string userAgent = null)
        {
            try
            {
                // Check if auditing is enabled for this entity type and action
                var auditConfig = await GetAuditConfigurationAsync(entityType, action);
                if (!auditConfig.IsEnabled)
                {
                    return;
                }

                var auditLogRepo = _unitOfWork.GetRepository<AuditLog>();

                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Timestamp = DateTime.UtcNow,
                    SessionId = GenerateSessionId(),
                    Source = DetermineSource(userAgent),
                    Category = DetermineCategory(action, entityType),
                    Severity = DetermineSeverity(action, entityType),
                    IsDeleted = false,
                    RetentionDate = DateTime.UtcNow.AddDays(auditConfig.RetentionDays)
                };

                // Process old and new values based on configuration
                if (auditConfig.IncludeOldValues && oldValues != null)
                {
                    auditLog.OldValues = await ProcessSensitiveDataAsync(oldValues, auditConfig.SensitiveFields);
                }

                if (auditConfig.IncludeNewValues && newValues != null)
                {
                    auditLog.NewValues = await ProcessSensitiveDataAsync(newValues, auditConfig.SensitiveFields);
                }

                // Add metadata
                auditLog.Metadata = new Dictionary<string, object>
                {
                    { "LogLevel", auditConfig.LogLevel },
                    { "ConfigId", auditConfig.Id },
                    { "Checksum", await GenerateChecksumAsync(auditLog) },
                    { "Version", "1.0" }
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

                // Audit logging failures should not break the main application flow
                // But we should log this failure for monitoring
                await LogInternalErrorAsync("AuditLogFailure", ex.Message, userId, ipAddress);
            }
        }

        public async Task LogSecurityEventAsync(Guid? userId, string eventType, string description,
            string severity = "medium", string ipAddress = null, Dictionary<string, object> metadata = null)
        {
            try
            {
                var securityAuditRepo = _unitOfWork.GetRepository<SecurityAuditLog>();

                var securityLog = new SecurityAuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EventType = eventType,
                    Description = description,
                    Severity = severity,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = ipAddress,
                    Source = "system",
                    ThreatLevel = DetermineThreatLevel(eventType, severity),
                    RequiresInvestigation = RequiresInvestigation(severity, eventType),
                    InvestigationStatus = "new",
                    EventData = metadata ?? new Dictionary<string, object>(),
                    IsArchived = false
                };

                // Add additional metadata
                securityLog.Metadata = new Dictionary<string, object>
                {
                    { "AutoGenerated", true },
                    { "Source", "AuditService" },
                    { "Checksum", await GenerateSecurityChecksumAsync(securityLog) },
                    { "Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                };

                await securityAuditRepo.InsertAsync(securityLog);
                await _unitOfWork.CommitAsync();

                // Also create a regular audit log for security events
                await LogAsync(userId, "SecurityEvent", "SecurityAuditLog", securityLog.Id.ToString(),
                    null, new { EventType = eventType, Severity = severity }, ipAddress);

                _logger.LogWarning("Security event logged: {EventType} for user {UserId} with severity {Severity}",
                    eventType, userId, severity);

                // Trigger alerts for high severity events
                if (severity == "high" || severity == "critical")
                {
                    await TriggerSecurityAlertAsync(securityLog);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging security event {EventType} for user {UserId}", eventType, userId);

                // For security events, we should try alternative logging methods
                await LogCriticalErrorAsync("SecurityLogFailure", ex.Message, userId, ipAddress);
            }
        }

        public async Task<List<AuditLog>> GetUserAuditLogsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null, int limit = 100)
        {
            try
            {
                var auditLogRepo = _unitOfWork.GetRepository<AuditLog>();

                var predicate = BuildUserAuditPredicate(userId, fromDate, toDate);

                var auditLogs = await auditLogRepo.GetListAsync(
                    predicate: predicate,
                    orderBy: logs => logs.OrderByDescending(l => l.Timestamp),
                    include: null);

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

                var predicate = BuildSystemAuditPredicate(fromDate, toDate);

                var auditLogs = await auditLogRepo.GetListAsync(
                    predicate: predicate,
                    orderBy: logs => logs.OrderByDescending(l => l.Timestamp),
                    include: null);

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

                var predicate = BuildSearchAuditPredicate(searchTerm, fromDate, toDate);

                var auditLogs = await auditLogRepo.GetListAsync(
                    predicate: predicate,
                    orderBy: logs => logs.OrderByDescending(l => l.Timestamp),
                    include: null);

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
                _logger.LogInformation("Starting audit log cleanup for logs older than {RetentionDays} days", retentionDays);

                var auditLogRepo = _unitOfWork.GetRepository<AuditLog>();
                var securityAuditRepo = _unitOfWork.GetRepository<SecurityAuditLog>();

                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                // Get logs to be cleaned up
                var expiredAuditLogs = await auditLogRepo.GetListAsync(
                    predicate: log => log.RetentionDate.HasValue && log.RetentionDate.Value < DateTime.UtcNow && !log.IsDeleted);

                var expiredSecurityLogs = await securityAuditRepo.GetListAsync(
                    predicate: log => log.Timestamp < cutoffDate && !log.IsArchived);

                // Archive security logs instead of deleting them
                foreach (var securityLog in expiredSecurityLogs)
                {
                    securityLog.IsArchived = true;
                    securityLog.ArchiveDate = DateTime.UtcNow;
                    securityAuditRepo.UpdateAsync(securityLog);
                }

                // Soft delete regular audit logs
                foreach (var auditLog in expiredAuditLogs)
                {
                    auditLog.IsDeleted = true;
                    auditLogRepo.UpdateAsync(auditLog);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Audit log cleanup completed. Archived {SecurityLogCount} security logs, soft-deleted {AuditLogCount} audit logs",
                    expiredSecurityLogs.Count, expiredAuditLogs.Count);

                // Log the cleanup operation itself
                await LogAsync(null, "CleanupOldLogs", "AuditService", "bulk_cleanup",
                    null, new
                    {
                        RetentionDays = retentionDays,
                        ArchivedSecurityLogs = expiredSecurityLogs.Count,
                        DeletedAuditLogs = expiredAuditLogs.Count
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during audit log cleanup");
                await LogInternalErrorAsync("AuditCleanupFailure", ex.Message, null, null);
            }
        }

        // Private helper methods
        private async Task<AuditConfiguration> GetAuditConfigurationAsync(string entityType, string action)
        {
            try
            {
                var configRepo = _unitOfWork.GetRepository<AuditConfiguration>();
                var config = await configRepo.SingleOrDefaultAsync(predicate:
                    c => c.EntityType == entityType &&
                         (c.ActionType == action || c.ActionType == "*") &&
                         c.IsActive);

                if (config != null)
                {
                    return new AuditConfiguration
                    {
                        Id = config.Id,
                        EntityType = config.EntityType,
                        ActionType = config.ActionType,
                        IsEnabled = config.IsEnabled,
                        RetentionDays = config.RetentionDays,
                        LogLevel = config.LogLevel,
                        IncludeOldValues = config.IncludeOldValues,
                        IncludeNewValues = config.IncludeNewValues,
                        RequireApproval = config.RequireApproval,
                        SensitiveFields = config.SensitiveFields
                    };
                }

                // Return default configuration if none found
                if (DefaultConfigurations.TryGetValue(entityType, out var defaultConfig))
                {
                    return defaultConfig;
                }

                return new AuditConfiguration
                {
                    EntityType = entityType,
                    ActionType = "*",
                    IsEnabled = true,
                    RetentionDays = 365,
                    LogLevel = "standard",
                    IncludeOldValues = true,
                    IncludeNewValues = true,
                    RequireApproval = false,
                    SensitiveFields = new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting audit configuration, using default");
                return DefaultConfigurations.GetValueOrDefault(entityType, DefaultConfigurations["ChatMessage"]);
            }
        }

        private async Task<string> ProcessSensitiveDataAsync(object data, List<string> sensitiveFields)
        {
            try
            {
                if (data == null)
                    return null;

                var jsonString = JsonSerializer.Serialize(data, _jsonOptions);

                if (!sensitiveFields.Any())
                    return jsonString;

                // Parse JSON and mask sensitive fields
                var jsonDocument = JsonDocument.Parse(jsonString);
                var maskedData = new Dictionary<string, object>();

                foreach (var property in jsonDocument.RootElement.EnumerateObject())
                {
                    if (sensitiveFields.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        maskedData[property.Name] = MaskSensitiveValue(property.Value.ToString());
                    }
                    else
                    {
                        maskedData[property.Name] = property.Value.ToString();
                    }
                }

                return JsonSerializer.Serialize(maskedData, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing sensitive data, returning masked placeholder");
                return "[SENSITIVE_DATA_PROCESSING_ERROR]";
            }
        }

        private string MaskSensitiveValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Length <= 4)
                return new string('*', value.Length);

            return value.Substring(0, 2) + new string('*', value.Length - 4) + value.Substring(value.Length - 2);
        }

        private async Task<string> GenerateChecksumAsync(AuditLog auditLog)
        {
            try
            {
                var checksumData = $"{auditLog.UserId}|{auditLog.Action}|{auditLog.EntityType}|{auditLog.EntityId}|{auditLog.Timestamp:O}";
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(checksumData));
                return Convert.ToHexString(hash).ToLower();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating checksum for audit log");
                return "checksum_error";
            }
        }

        private async Task<string> GenerateSecurityChecksumAsync(SecurityAuditLog securityLog)
        {
            try
            {
                var checksumData = $"{securityLog.UserId}|{securityLog.EventType}|{securityLog.Severity}|{securityLog.Timestamp:O}";
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(checksumData));
                return Convert.ToHexString(hash).ToLower();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating security checksum");
                return "checksum_error";
            }
        }

        private string GenerateSessionId()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            return session?.Id ?? Guid.NewGuid().ToString("N")[..16];
        }

        private string DetermineSource(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return "unknown";

            if (userAgent.Contains("Mobile"))
                return "mobile";
            else if (userAgent.Contains("API") || userAgent.Contains("Bot"))
                return "api";
            else
                return "web";
        }

        private string DetermineCategory(string action, string entityType)
        {
            var securityActions = new[] { "Login", "Logout", "PasswordChange", "SecurityEvent", "AccessDenied" };
            var systemActions = new[] { "SystemStartup", "SystemShutdown", "ConfigurationChange" };

            if (securityActions.Contains(action, StringComparer.OrdinalIgnoreCase))
                return "security_event";
            else if (systemActions.Contains(action, StringComparer.OrdinalIgnoreCase))
                return "system_event";
            else
                return "user_action";
        }

        private string DetermineSeverity(string action, string entityType)
        {
            var highSeverityActions = new[] { "Delete", "SecurityEvent", "AccessDenied", "PasswordChange" };
            var mediumSeverityActions = new[] { "Update", "Create", "Login", "Logout" };

            if (highSeverityActions.Contains(action, StringComparer.OrdinalIgnoreCase))
                return "high";
            else if (mediumSeverityActions.Contains(action, StringComparer.OrdinalIgnoreCase))
                return "medium";
            else
                return "low";
        }

        private string DetermineThreatLevel(string eventType, string severity)
        {
            var criticalEvents = new[] { "DataBreach", "SystemCompromise", "UnauthorizedAccess" };
            var highThreatEvents = new[] { "SecurityViolation", "SuspiciousActivity", "MultipleFailedLogins" };

            if (severity == "critical" || criticalEvents.Contains(eventType, StringComparer.OrdinalIgnoreCase))
                return "critical";
            else if (severity == "high" || highThreatEvents.Contains(eventType, StringComparer.OrdinalIgnoreCase))
                return "high";
            else if (severity == "medium")
                return "medium";
            else
                return "low";
        }

        private bool RequiresInvestigation(string severity, string eventType)
        {
            var investigationRequired = new[] { "critical", "high" };
            var autoInvestigateEvents = new[] { "DataBreach", "SystemCompromise", "SecurityViolation" };

            return investigationRequired.Contains(severity, StringComparer.OrdinalIgnoreCase) ||
                   autoInvestigateEvents.Contains(eventType, StringComparer.OrdinalIgnoreCase);
        }

        private async Task TriggerSecurityAlertAsync(SecurityAuditLog securityLog)
        {
            try
            {
                // In a real implementation, this would trigger alerts via:
                // - Email notifications
                // - Slack/Teams notifications
                // - SMS alerts
                // - Dashboard alerts
                // - SIEM integration

                _logger.LogWarning("SECURITY ALERT: {EventType} for user {UserId} - {Description}",
                    securityLog.EventType, securityLog.UserId, securityLog.Description);

                // For now, just log at critical level
                _logger.LogCritical("High severity security event detected: {SecurityLogId}", securityLog.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering security alert for {SecurityLogId}", securityLog.Id);
            }
        }

        private async Task LogInternalErrorAsync(string errorType, string message, Guid? userId, string ipAddress)
        {
            try
            {
                // Use a separate error logging mechanism that doesn't depend on the main audit system
                _logger.LogError("AUDIT_SYSTEM_ERROR: {ErrorType} - {Message} for user {UserId}",
                    errorType, message, userId);

                // Could also write to a separate error log file or external logging service
            }
            catch (Exception ex)
            {
                // Last resort logging
                _logger.LogCritical(ex, "CRITICAL: Audit system completely failed to log error");
            }
        }

        private async Task LogCriticalErrorAsync(string errorType, string message, Guid? userId, string ipAddress)
        {
            try
            {
                _logger.LogCritical("CRITICAL_AUDIT_ERROR: {ErrorType} - {Message} for user {UserId}",
                    errorType, message, userId);

                // Write to event log or external monitoring system
                // This should trigger immediate alerts
            }
            catch (Exception ex)
            {
                // Absolute last resort - write to console or event log
                Console.WriteLine($"CRITICAL AUDIT FAILURE: {ex.Message}");
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
            return log => !log.IsDeleted &&
                         log.Category == "system_event" &&
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

    // Extension methods for audit convenience
    public static class AuditExtensions
    {
        public static async Task LogCreateAsync<T>(this IAuditService auditService,
            Guid? userId, T entity, string ipAddress = null, string userAgent = null) where T : class
        {
            var entityType = typeof(T).Name;
            var entityId = GetEntityId(entity);
            await auditService.LogAsync(userId, "Create", entityType, entityId, null, entity, ipAddress, userAgent);
        }

        public static async Task LogUpdateAsync<T>(this IAuditService auditService,
            Guid? userId, T oldEntity, T newEntity, string ipAddress = null, string userAgent = null) where T : class
        {
            var entityType = typeof(T).Name;
            var entityId = GetEntityId(newEntity);
            await auditService.LogAsync(userId, "Update", entityType, entityId, oldEntity, newEntity, ipAddress, userAgent);
        }

        public static async Task LogDeleteAsync<T>(this IAuditService auditService,
            Guid? userId, T entity, string ipAddress = null, string userAgent = null) where T : class
        {
            var entityType = typeof(T).Name;
            var entityId = GetEntityId(entity);
            await auditService.LogAsync(userId, "Delete", entityType, entityId, entity, null, ipAddress, userAgent);
        }

        private static string GetEntityId<T>(T entity)
        {
            var idProperty = typeof(T).GetProperty("Id");
            return idProperty?.GetValue(entity)?.ToString() ?? "unknown";
        }
    }
}
