using Document.API.Models;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service for synchronizing permissions between database and Google Drive
    /// Ensures consistency between local folder permissions and Google Drive sharing settings
    /// </summary>
    public interface IPermissionSyncService
    {
        /// <summary>
        /// Synchronize all permissions for a specific folder
        /// </summary>
        /// <param name="folderId">Database folder ID</param>
        /// <param name="forceSync">Force synchronization even if already in sync</param>
        /// <returns>Synchronization result</returns>
        Task<PermissionSyncResult> SyncFolderPermissionsAsync(string folderId, bool forceSync = false);

        /// <summary>
        /// Synchronize permissions for all folders in a department
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <param name="includePublic">Include public folders</param>
        /// <returns>Bulk synchronization result</returns>
        Task<BulkPermissionSyncResult> SyncDepartmentPermissionsAsync(string departmentId, bool includePublic = true);

        /// <summary>
        /// Verify permission consistency between database and Google Drive
        /// </summary>
        /// <param name="folderId">Database folder ID</param>
        /// <returns>Permission verification result</returns>
        Task<PermissionVerificationResult> VerifyPermissionConsistencyAsync(string folderId);

        /// <summary>
        /// Detect and report permission mismatches across all folders
        /// </summary>
        /// <param name="departmentId">Optional department filter</param>
        /// <returns>List of permission mismatches</returns>
        Task<List<PermissionMismatch>> DetectPermissionMismatchesAsync(string? departmentId = null);

        /// <summary>
        /// Repair permission mismatches automatically where possible
        /// </summary>
        /// <param name="mismatches">List of mismatches to repair</param>
        /// <param name="dryRun">If true, only simulate repairs without making changes</param>
        /// <returns>Repair result</returns>
        Task<PermissionRepairResult> RepairPermissionMismatchesAsync(List<PermissionMismatch> mismatches, bool dryRun = true);

        /// <summary>
        /// Grant permission in both database and Google Drive
        /// </summary>
        /// <param name="folderId">Database folder ID</param>
        /// <param name="userEmail">User email</param>
        /// <param name="permissionType">Permission type</param>
        /// <param name="departmentId">User's department ID</param>
        /// <returns>Permission grant result</returns>
        Task<PermissionSyncResult> GrantPermissionAsync(string folderId, string userEmail, string permissionType, string departmentId);

        /// <summary>
        /// Revoke permission from both database and Google Drive
        /// </summary>
        /// <param name="folderId">Database folder ID</param>
        /// <param name="userEmail">User email</param>
        /// <returns>Permission revoke result</returns>
        Task<PermissionSyncResult> RevokePermissionAsync(string folderId, string userEmail);

        /// <summary>
        /// Synchronize permissions for a specific user across all accessible folders
        /// </summary>
        /// <param name="userEmail">User email</param>
        /// <param name="departmentId">User's department ID</param>
        /// <returns>User permission sync result</returns>
        Task<UserPermissionSyncResult> SyncUserPermissionsAsync(string userEmail, string departmentId);

        /// <summary>
        /// Get comprehensive permission status for a folder
        /// </summary>
        /// <param name="folderId">Database folder ID</param>
        /// <returns>Detailed permission status</returns>
        Task<FolderPermissionStatus> GetFolderPermissionStatusAsync(string folderId);

        /// <summary>
        /// Perform health check on permission synchronization system
        /// </summary>
        /// <returns>Health check result</returns>
        Task<PermissionSyncHealthResult> PerformHealthCheckAsync();

        /// <summary>
        /// Get synchronization statistics
        /// </summary>
        /// <param name="departmentId">Optional department filter</param>
        /// <returns>Sync statistics</returns>
        Task<PermissionSyncStatistics> GetSyncStatisticsAsync(string? departmentId = null);
    }

    #region Result Models

    /// <summary>
    /// Result of permission synchronization operation
    /// </summary>
    public class PermissionSyncResult
    {
        public bool Success { get; set; }
        public string FolderId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int PermissionsSynced { get; set; }
        public int PermissionsFailed { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Result of bulk permission synchronization
    /// </summary>
    public class BulkPermissionSyncResult
    {
        public bool Success { get; set; }
        public string DepartmentId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int FoldersProcessed { get; set; }
        public int FoldersSuccessful { get; set; }
        public int FoldersFailed { get; set; }
        public int TotalPermissionsSynced { get; set; }
        public List<PermissionSyncResult> FolderResults { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration { get; set; }
        public string Summary => $"Processed {FoldersProcessed} folders: {FoldersSuccessful} successful, {FoldersFailed} failed";
    }

    /// <summary>
    /// Result of permission verification
    /// </summary>
    public class PermissionVerificationResult
    {
        public bool IsConsistent { get; set; }
        public string FolderId { get; set; } = string.Empty;
        public int DatabasePermissions { get; set; }
        public int GoogleDrivePermissions { get; set; }
        public List<PermissionMismatch> Mismatches { get; set; } = new();
        public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
        public string Summary => $"Database: {DatabasePermissions}, Google Drive: {GoogleDrivePermissions}, Mismatches: {Mismatches.Count}";
    }

    /// <summary>
    /// Represents a permission mismatch between database and Google Drive
    /// </summary>
    public class PermissionMismatch
    {
        public string FolderId { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public PermissionMismatchType MismatchType { get; set; }
        public string? DatabasePermission { get; set; }
        public string? GoogleDrivePermission { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool CanAutoRepair { get; set; }
        public string RepairAction { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of permission mismatches
    /// </summary>
    public enum PermissionMismatchType
    {
        DatabaseOnly,
        GoogleDriveOnly,
        PermissionLevelMismatch,
        InvalidPermission,
        ExpiredPermission
    }

    /// <summary>
    /// Result of permission repair operation
    /// </summary>
    public class PermissionRepairResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int MismatchesRepaired { get; set; }
        public int MismatchesFailed { get; set; }
        public int MismatchesSkipped { get; set; }
        public List<string> RepairedItems { get; set; } = new();
        public List<string> FailedItems { get; set; } = new();
        public List<string> SkippedItems { get; set; } = new();
        public bool RequiresManualIntervention { get; set; }
        public DateTime RepairedAt { get; set; } = DateTime.UtcNow;
        public string Summary => $"Repaired: {MismatchesRepaired}, Failed: {MismatchesFailed}, Skipped: {MismatchesSkipped}";
    }

    /// <summary>
    /// User-specific permission synchronization result
    /// </summary>
    public class UserPermissionSyncResult
    {
        public bool Success { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string DepartmentId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int FoldersWithAccess { get; set; }
        public int PermissionsSynced { get; set; }
        public List<string> AccessibleFolders { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Comprehensive permission status for a folder
    /// </summary>
    public class FolderPermissionStatus
    {
        public string FolderId { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public string? GoogleDriveFolderId { get; set; }
        public bool IsInSync { get; set; }
        public int DatabasePermissionCount { get; set; }
        public int GoogleDrivePermissionCount { get; set; }
        public List<PermissionDetail> DatabasePermissions { get; set; } = new();
        public List<PermissionDetail> GoogleDrivePermissions { get; set; } = new();
        public List<PermissionMismatch> Mismatches { get; set; } = new();
        public DateTime LastSyncedAt { get; set; }
        public string SyncStatus { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detailed permission information
    /// </summary>
    public class PermissionDetail
    {
        public string UserEmail { get; set; } = string.Empty;
        public string PermissionType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // "Database" or "GoogleDrive"
        public DateTime? GrantedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Health check result for permission synchronization system
    /// </summary>
    public class PermissionSyncHealthResult
    {
        public bool IsHealthy { get; set; }
        public List<HealthCheckItem> Checks { get; set; } = new();
        public int TotalFolders { get; set; }
        public int FoldersInSync { get; set; }
        public int FoldersOutOfSync { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public string Summary => $"Health: {(IsHealthy ? "Good" : "Issues")}, Sync: {FoldersInSync}/{TotalFolders}";
    }

    /// <summary>
    /// Permission synchronization statistics
    /// </summary>
    public class PermissionSyncStatistics
    {
        public string? DepartmentId { get; set; }
        public int TotalFolders { get; set; }
        public int FoldersWithPermissions { get; set; }
        public int TotalPermissions { get; set; }
        public int SyncedPermissions { get; set; }
        public int OutOfSyncPermissions { get; set; }
        public double SyncPercentage => TotalPermissions > 0 ? (double)SyncedPermissions / TotalPermissions * 100 : 0;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, int> PermissionTypeBreakdown { get; set; } = new();
        public Dictionary<string, int> MismatchTypeBreakdown { get; set; } = new();
    }

    #endregion
}
