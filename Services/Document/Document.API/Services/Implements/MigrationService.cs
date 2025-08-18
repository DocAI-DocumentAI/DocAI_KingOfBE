using Document.API.Services.Interfaces;
using Document.Infrastructure.Repository.Interfaces;
using Document.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service for migrating documents from Azure Blob Storage to Google Drive
    /// MIGRATION COMPLETE - Azure dependencies commented out
    /// </summary>
    public class MigrationService : IMigrationService
    {
        // Azure service commented out - migration complete
        // private readonly IAzureStorageService _azureStorageService;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IGoogleDriveOAuthService _googleDriveOAuthService;
        private readonly IRedisService _redisService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MigrationService> _logger;

        private const string MIGRATION_STATUS_KEY = "google_drive_migration_status";
        private const string MIGRATION_JOB_PREFIX = "migration_job:";

        public MigrationService(
            // IAzureStorageService azureStorageService, // Commented out - migration complete
            IGoogleDriveService googleDriveService,
            IGoogleDriveOAuthService googleDriveOAuthService,
            IRedisService redisService,
            IUnitOfWork unitOfWork,
            ILogger<MigrationService> logger)
        {
            // _azureStorageService = azureStorageService; // Commented out - migration complete
            _googleDriveService = googleDriveService;
            _googleDriveOAuthService = googleDriveOAuthService;
            _redisService = redisService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<MigrationResult> MigrateFileAsync(string documentVersionId)
        {
            try
            {
                _logger.LogInformation("Starting migration for document version {DocumentVersionId}", documentVersionId);

                // Get document version from database
                var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.Id == documentVersionId,
                        include: i => i.Include(dv => dv.DocumentFile)
                    );

                if (documentVersion == null)
                {
                    return new MigrationResult
                    {
                        Success = false,
                        DocumentVersionId = documentVersionId,
                        Message = "Document version not found",
                        ErrorDetails = $"No document version found with ID {documentVersionId}"
                    };
                }

                // Check if already migrated (has Google Drive file ID)
                if (!string.IsNullOrEmpty(documentVersion.GoogleDriveFileId))
                {
                    _logger.LogInformation("Document version {DocumentVersionId} already migrated", documentVersionId);
                    return new MigrationResult
                    {
                        Success = true,
                        DocumentVersionId = documentVersionId,
                        OriginalFilePath = documentVersion.FilePath,
                        NewFileId = documentVersion.GoogleDriveFileId,
                        Message = "File already migrated",
                        MigratedAt = DateTime.UtcNow
                    };
                }

                // MIGRATION COMPLETE - Azure download commented out
                // var azureStream = await _azureStorageService.DownloadFileAsync(documentVersion.FilePath);
                // if (azureStream == null)
                // {
                //     return new MigrationResult
                //     {
                //         Success = false,
                //         DocumentVersionId = documentVersionId,
                //         OriginalFilePath = documentVersion.FilePath,
                //         Message = "Failed to download file from Azure",
                //         ErrorDetails = "File not found in Azure Blob Storage"
                //     };
                // }

                // Migration is complete - all files should already be in Google Drive
                return new MigrationResult
                {
                    Success = false,
                    DocumentVersionId = documentVersionId,
                    OriginalFilePath = documentVersion.FilePath,
                    Message = "Migration already complete - all files are in Google Drive",
                    ErrorDetails = "No Azure migration needed"
                };

                // MIGRATION COMPLETE - Google Drive upload logic commented out
                // var formFile = CreateFormFileFromStream(azureStream, documentVersion.FileName, "application/octet-stream");
                //
                // // Determine folder and access settings
                // var folder = DetermineFolderFromFilePath(documentVersion.FilePath);
                // var departmentId = documentVersion.DocumentFile?.DepartmentId;
                // var isPublic = documentVersion.IsPublic;
                //
                // // Upload to Google Drive
                // var uploadResponse = await _googleDriveService.UploadFileAsync(formFile, folder, departmentId, isPublic);
                //
                // // Update database with Google Drive file ID
                // documentVersion.GoogleDriveFileId = uploadResponse.FileId;
                // documentVersion.LastUpdatedTime = DateTime.UtcNow;
                //
                // await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(documentVersion);
                // await _unitOfWork.CommitAsync();

                // MIGRATION COMPLETE - Success logging commented out
                // _logger.LogInformation("Successfully migrated document version {DocumentVersionId} to Google Drive with file ID {FileId}",
                //     documentVersionId, uploadResponse.FileId);
                //
                // return new MigrationResult
                // {
                //     Success = true,
                //     DocumentVersionId = documentVersionId,
                //     OriginalFilePath = documentVersion.FilePath,
                //     NewFileId = uploadResponse.FileId,
                //     Md5Hash = uploadResponse.Md5Hash,
                //     FileSize = uploadResponse.FileSize,
                //     Message = "File migrated successfully",
                //     MigratedAt = DateTime.UtcNow
                // };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating document version {DocumentVersionId}", documentVersionId);
                return new MigrationResult
                {
                    Success = false,
                    DocumentVersionId = documentVersionId,
                    Message = "Migration failed",
                    ErrorDetails = ex.Message
                };
            }
        }

        public async Task<MigrationSummary> MigrateFolderAsync(string folder)
        {
            var summary = new MigrationSummary
            {
                StartedAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Starting migration for folder: {Folder}", folder);

                // Get all document versions in the specified folder
                var documentVersions = await _unitOfWork.GetRepository<DocumentVersion>()
                    .GetListAsync(
                        predicate: dv => dv.FilePath.StartsWith(folder + "/"),
                        include: i => i.Include(dv => dv.DocumentFile)
                    );

                summary.TotalFiles = documentVersions.Count();

                foreach (var documentVersion in documentVersions)
                {
                    var result = await MigrateFileAsync(documentVersion.Id);
                    summary.Results.Add(result);

                    if (result.Success)
                    {
                        summary.SuccessfulMigrations++;
                    }
                    else
                    {
                        summary.FailedMigrations++;
                        summary.Errors.Add($"Document {documentVersion.Id}: {result.ErrorDetails}");
                    }

                    // Update migration status
                    await UpdateMigrationStatusAsync("in_progress", new
                    {
                        Folder = folder,
                        ProcessedFiles = summary.SuccessfulMigrations + summary.FailedMigrations,
                        TotalFiles = summary.TotalFiles,
                        CurrentFile = documentVersion.FileName
                    });
                }

                summary.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Completed migration for folder {Folder}. Success: {Success}, Failed: {Failed}", 
                    folder, summary.SuccessfulMigrations, summary.FailedMigrations);

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating folder {Folder}", folder);
                summary.CompletedAt = DateTime.UtcNow;
                summary.Errors.Add($"Folder migration error: {ex.Message}");
                return summary;
            }
        }

        public async Task<MigrationSummary> MigrateAllDocumentsAsync()
        {
            var overallSummary = new MigrationSummary
            {
                StartedAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Starting migration of all documents");

                // Check if Google Drive is available
                var hasValidTokens = await _googleDriveOAuthService.HasValidCompanyTokensAsync();
                if (!hasValidTokens)
                {
                    throw new InvalidOperationException("Google Drive company account not authorized. Complete setup first.");
                }

                // Initialize company folders
                await _googleDriveService.InitializeCompanyFoldersAsync();

                // Migrate only drafts folder (approved documents go to functional folders)
                var folders = new[] { "drafts" };
                
                foreach (var folder in folders)
                {
                    var folderSummary = await MigrateFolderAsync(folder);
                    
                    overallSummary.TotalFiles += folderSummary.TotalFiles;
                    overallSummary.SuccessfulMigrations += folderSummary.SuccessfulMigrations;
                    overallSummary.FailedMigrations += folderSummary.FailedMigrations;
                    overallSummary.Results.AddRange(folderSummary.Results);
                    overallSummary.Errors.AddRange(folderSummary.Errors);
                }

                overallSummary.CompletedAt = DateTime.UtcNow;

                // Update final migration status
                await UpdateMigrationStatusAsync("completed", new
                {
                    TotalFiles = overallSummary.TotalFiles,
                    SuccessfulMigrations = overallSummary.SuccessfulMigrations,
                    FailedMigrations = overallSummary.FailedMigrations,
                    Duration = overallSummary.Duration.TotalMinutes
                });

                _logger.LogInformation("Completed migration of all documents. Total: {Total}, Success: {Success}, Failed: {Failed}", 
                    overallSummary.TotalFiles, overallSummary.SuccessfulMigrations, overallSummary.FailedMigrations);

                return overallSummary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during complete migration");
                overallSummary.CompletedAt = DateTime.UtcNow;
                overallSummary.Errors.Add($"Complete migration error: {ex.Message}");

                await UpdateMigrationStatusAsync("failed", new
                {
                    Error = ex.Message,
                    ProcessedFiles = overallSummary.SuccessfulMigrations + overallSummary.FailedMigrations,
                    TotalFiles = overallSummary.TotalFiles
                });

                return overallSummary;
            }
        }

        public async Task<MigrationStatus> GetMigrationStatusAsync()
        {
            try
            {
                var statusJson = await _redisService.GetStringAsync(MIGRATION_STATUS_KEY);
                if (string.IsNullOrEmpty(statusJson))
                {
                    return new MigrationStatus
                    {
                        Status = "not_started"
                    };
                }

                return System.Text.Json.JsonSerializer.Deserialize<MigrationStatus>(statusJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting migration status");
                return new MigrationStatus
                {
                    Status = "error"
                };
            }
        }

        public async Task<string> StartMigrationAsync(int batchSize = 10)
        {
            var jobId = Guid.NewGuid().ToString();
            
            try
            {
                _logger.LogInformation("Starting background migration with job ID {JobId}", jobId);

                // Update status to in_progress
                await UpdateMigrationStatusAsync("in_progress", new
                {
                    JobId = jobId,
                    BatchSize = batchSize,
                    StartedAt = DateTime.UtcNow
                });

                // Start migration in background (you might want to use a background service for this)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await MigrateAllDocumentsAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background migration failed for job {JobId}", jobId);
                        await UpdateMigrationStatusAsync("failed", new { JobId = jobId, Error = ex.Message });
                    }
                });

                return jobId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting migration job {JobId}", jobId);
                await UpdateMigrationStatusAsync("failed", new { JobId = jobId, Error = ex.Message });
                throw;
            }
        }

        public async Task CancelMigrationAsync(string migrationJobId)
        {
            try
            {
                _logger.LogInformation("Cancelling migration job {JobId}", migrationJobId);
                
                await UpdateMigrationStatusAsync("cancelled", new
                {
                    JobId = migrationJobId,
                    CancelledAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling migration job {JobId}", migrationJobId);
                throw;
            }
        }

        public async Task<VerificationResult> VerifyMigrationAsync(string documentVersionId)
        {
            try
            {
                _logger.LogInformation("Verifying migration for document version {DocumentVersionId}", documentVersionId);

                var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(predicate: dv => dv.Id == documentVersionId);

                if (documentVersion == null)
                {
                    return new VerificationResult
                    {
                        IsValid = false,
                        DocumentVersionId = documentVersionId,
                        Message = "Document version not found"
                    };
                }

                if (string.IsNullOrEmpty(documentVersion.GoogleDriveFileId))
                {
                    return new VerificationResult
                    {
                        IsValid = false,
                        DocumentVersionId = documentVersionId,
                        Message = "File not migrated to Google Drive"
                    };
                }

                // MIGRATION COMPLETE - Azure verification commented out
                // var azureExists = await _azureStorageService.FileExistsAsync(documentVersion.FilePath);
                var googleExists = await _googleDriveService.FileExistsAsync(documentVersion.GoogleDriveFileId);

                if (!googleExists) // Migration complete - only check Google Drive
                {
                    return new VerificationResult
                    {
                        IsValid = false,
                        DocumentVersionId = documentVersionId,
                        Message = $"File missing - Google: {googleExists} (Migration complete - Azure not checked)"
                    };
                }

                // Compare file sizes and hashes if possible
                // Note: This is a simplified verification - you might want to download and compare actual content
                return new VerificationResult
                {
                    IsValid = true,
                    DocumentVersionId = documentVersionId,
                    Message = "Migration verified successfully",
                    VerifiedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying migration for document version {DocumentVersionId}", documentVersionId);
                return new VerificationResult
                {
                    IsValid = false,
                    DocumentVersionId = documentVersionId,
                    Message = $"Verification failed: {ex.Message}"
                };
            }
        }

        public async Task<MigrationResult> RollbackFileAsync(string documentVersionId)
        {
            try
            {
                _logger.LogInformation("Rolling back document version {DocumentVersionId}", documentVersionId);

                var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(predicate: dv => dv.Id == documentVersionId);

                if (documentVersion == null)
                {
                    return new MigrationResult
                    {
                        Success = false,
                        DocumentVersionId = documentVersionId,
                        Message = "Document version not found"
                    };
                }

                if (string.IsNullOrEmpty(documentVersion.GoogleDriveFileId))
                {
                    return new MigrationResult
                    {
                        Success = false,
                        DocumentVersionId = documentVersionId,
                        Message = "File not migrated to Google Drive, nothing to rollback"
                    };
                }

                // Clear Google Drive file ID from database
                documentVersion.GoogleDriveFileId = null;
                documentVersion.LastUpdatedTime = DateTime.UtcNow;

                await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(documentVersion);
                await _unitOfWork.CommitAsync();

                // Optionally delete from Google Drive
                try
                {
                    await _googleDriveService.DeleteFileAsync(documentVersion.GoogleDriveFileId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file from Google Drive during rollback, but database updated");
                }

                _logger.LogInformation("Successfully rolled back document version {DocumentVersionId}", documentVersionId);

                return new MigrationResult
                {
                    Success = true,
                    DocumentVersionId = documentVersionId,
                    OriginalFilePath = documentVersion.FilePath,
                    Message = "File rolled back successfully",
                    MigratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rolling back document version {DocumentVersionId}", documentVersionId);
                return new MigrationResult
                {
                    Success = false,
                    DocumentVersionId = documentVersionId,
                    Message = "Rollback failed",
                    ErrorDetails = ex.Message
                };
            }
        }

        #region Private Helper Methods

        private async Task UpdateMigrationStatusAsync(string status, object data = null)
        {
            try
            {
                await _redisService.SetMigrationStatusAsync(status, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating migration status");
            }
        }

        private static IFormFile CreateFormFileFromStream(Stream stream, string fileName, string contentType)
        {
            return new StreamFormFile(stream, fileName, contentType);
        }

        private class StreamFormFile : IFormFile
        {
            private readonly Stream _stream;

            public StreamFormFile(Stream stream, string fileName, string contentType)
            {
                _stream = stream;
                FileName = fileName;
                ContentType = contentType;
                Length = stream.Length;
                Headers = new HeaderDictionary();
                Name = "file";
            }

            public string ContentType { get; set; }
            public string ContentDisposition { get; set; } = "";
            public IHeaderDictionary Headers { get; set; }
            public long Length { get; }
            public string Name { get; set; }
            public string FileName { get; set; }

            public Stream OpenReadStream() => _stream;

            public void CopyTo(Stream target) => _stream.CopyTo(target);

            public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
                => _stream.CopyToAsync(target, cancellationToken);
        }

        private static string DetermineFolderFromFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "drafts";

            var parts = filePath.Split('/');
            return parts.Length > 0 ? parts[0] : "drafts";
        }

        #endregion
    }
}
