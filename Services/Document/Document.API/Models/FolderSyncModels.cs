namespace Document.API.Models
{
    /// <summary>
    /// Result of folder synchronization verification between database and Google Drive
    /// </summary>
    public class FolderSyncVerificationResult
    {
        public bool IsInSync { get; set; }
        public int TotalFoldersChecked { get; set; }
        public int SyncIssuesFound { get; set; }
        public List<FolderSyncIssue> Issues { get; set; } = new();
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public string? DepartmentId { get; set; }
        public string Summary => $"Checked {TotalFoldersChecked} folders, found {SyncIssuesFound} sync issues";
    }

    /// <summary>
    /// Represents a synchronization issue between database and Google Drive
    /// </summary>
    public class FolderSyncIssue
    {
        public string FolderId { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public string? GoogleDriveFolderId { get; set; }
        public FolderSyncIssueType IssueType { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool CanAutoRepair { get; set; }
        public string? DepartmentId { get; set; }
        public string FullPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of folder synchronization issues
    /// </summary>
    public enum FolderSyncIssueType
    {
        /// <summary>
        /// Folder exists in database but not in Google Drive
        /// </summary>
        DatabaseOrphan,

        /// <summary>
        /// Folder exists in Google Drive but not in database
        /// </summary>
        GoogleDriveOrphan,

        /// <summary>
        /// Folder exists in both but has mismatched metadata
        /// </summary>
        MetadataMismatch,

        /// <summary>
        /// Folder has invalid Google Drive ID in database
        /// </summary>
        InvalidGoogleDriveId,

        /// <summary>
        /// Folder permissions are out of sync
        /// </summary>
        PermissionMismatch
    }

    /// <summary>
    /// Result of sync repair operations
    /// </summary>
    public class FolderSyncRepairResult
    {
        public bool Success { get; set; }
        public int IssuesRepaired { get; set; }
        public int IssuesRemaining { get; set; }
        public List<string> RepairedIssues { get; set; } = new();
        public List<string> FailedRepairs { get; set; } = new();
        public bool RequiresManualIntervention { get; set; }
        public string Summary => $"Repaired {IssuesRepaired} issues, {IssuesRemaining} remaining";
    }

    /// <summary>
    /// Health check result for Google Drive and folder system
    /// </summary>
    public class FolderSystemHealthResult
    {
        public bool IsHealthy { get; set; }
        public List<HealthCheckItem> Checks { get; set; } = new();
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public string Summary => IsHealthy ? "All systems healthy" : $"{Checks.Count(c => !c.Success)} issues found";
    }

    /// <summary>
    /// Individual health check item
    /// </summary>
    public class HealthCheckItem
    {
        public string Name { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string? Details { get; set; }
    }
}
