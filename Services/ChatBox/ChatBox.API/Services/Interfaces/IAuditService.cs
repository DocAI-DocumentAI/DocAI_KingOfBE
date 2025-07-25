using ChatBox.Domain.Models;

namespace ChatBox.API.Services.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(Guid? userId, string action, string entityType, string entityId,
            object oldValues = null, object newValues = null, string ipAddress = null, string userAgent = null);
        Task LogSecurityEventAsync(Guid? userId, string eventType, string description,
            string severity = "medium", string ipAddress = null, Dictionary<string, object> metadata = null);
        Task<List<AuditLog>> GetUserAuditLogsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null, int limit = 100);
        Task<List<AuditLog>> GetSystemAuditLogsAsync(DateTime? fromDate = null, DateTime? toDate = null, int limit = 1000);
        Task<List<AuditLog>> SearchAuditLogsAsync(string searchTerm, DateTime? fromDate = null, DateTime? toDate = null, int limit = 100);
        Task CleanupOldLogsAsync(int retentionDays = 90);
    }
}
