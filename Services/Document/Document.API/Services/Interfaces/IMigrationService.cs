namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service for migrating documents from Azure Blob Storage to Google Drive
    /// </summary>
    public interface IMigrationService
    {
        Task<MigrationResult> MigrateFileAsync(string documentVersionId);
        Task<MigrationSummary> MigrateFolderAsync(string folder);
        Task<MigrationSummary> MigrateAllDocumentsAsync();
        Task<MigrationStatus> GetMigrationStatusAsync();
        Task<string> StartMigrationAsync(int batchSize = 10);
        Task CancelMigrationAsync(string migrationJobId);
        Task<VerificationResult> VerifyMigrationAsync(string documentVersionId);
        Task<MigrationResult> RollbackFileAsync(string documentVersionId);
    }

    /// <summary>
    /// Result of a single file migration
    /// </summary>
    public class MigrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string DocumentVersionId { get; set; }
        public string OriginalFilePath { get; set; }
        public string NewFileId { get; set; }
        public string Md5Hash { get; set; }
        public long FileSize { get; set; }
        public DateTime MigratedAt { get; set; }
        public string ErrorDetails { get; set; }
    }

    /// <summary>
    /// Summary of migration operation
    /// </summary>
    public class MigrationSummary
    {
        public int TotalFiles { get; set; }
        public int SuccessfulMigrations { get; set; }
        public int FailedMigrations { get; set; }
        public int SkippedFiles { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan Duration => CompletedAt?.Subtract(StartedAt) ?? DateTime.UtcNow.Subtract(StartedAt);
        public List<MigrationResult> Results { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Current migration status
    /// </summary>
    public class MigrationStatus
    {
        public string Status { get; set; } // not_started, in_progress, completed, failed, cancelled
        public string JobId { get; set; }
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int SuccessfulMigrations { get; set; }
        public int FailedMigrations { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public string CurrentFile { get; set; }
        public double ProgressPercentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
        public List<string> RecentErrors { get; set; } = new();
    }

    /// <summary>
    /// Result of migration verification
    /// </summary>
    public class VerificationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public string DocumentVersionId { get; set; }
        public string AzureMd5Hash { get; set; }
        public string GoogleDriveMd5Hash { get; set; }
        public long AzureFileSize { get; set; }
        public long GoogleDriveFileSize { get; set; }
        public DateTime VerifiedAt { get; set; }
    }
}
