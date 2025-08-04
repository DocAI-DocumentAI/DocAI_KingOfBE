using Microsoft.AspNetCore.Mvc;
using Document.API.Services.Interfaces;
using Document.API.Attributes;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for managing migration from Azure Blob Storage to Google Drive
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [CustomAuthorize]
    public class MigrationController : ControllerBase
    {
        private readonly IMigrationService _migrationService;
        private readonly IGoogleDriveOAuthService _googleDriveOAuthService;
        private readonly ILogger<MigrationController> _logger;

        public MigrationController(
            IMigrationService migrationService,
            IGoogleDriveOAuthService googleDriveOAuthService,
            ILogger<MigrationController> logger)
        {
            _migrationService = migrationService;
            _googleDriveOAuthService = googleDriveOAuthService;
            _logger = logger;
        }

        /// <summary>
        /// Get current migration status
        /// </summary>
        /// <returns>Migration status information</returns>
        [HttpGet("status")]
        public async Task<IActionResult> GetMigrationStatus()
        {
            try
            {
                var status = await _migrationService.GetMigrationStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting migration status");
                return StatusCode(500, "Error getting migration status");
            }
        }

        /// <summary>
        /// Start migration of all documents from Azure to Google Drive
        /// </summary>
        /// <param name="batchSize">Number of files to process in each batch</param>
        /// <returns>Migration job information</returns>
        [HttpPost("start")]
        public async Task<IActionResult> StartMigration([FromQuery] int batchSize = 10)
        {
            try
            {
                // Check if Google Drive is properly configured
                var hasValidTokens = await _googleDriveOAuthService.HasValidCompanyTokensAsync();
                if (!hasValidTokens)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Google Drive company account not authorized. Complete setup first.",
                        RequiredAction = "Use /api/googledrive-setup/company-auth-url to authorize company account"
                    });
                }

                var jobId = await _migrationService.StartMigrationAsync(batchSize);

                return Ok(new
                {
                    Success = true,
                    JobId = jobId,
                    Message = "Migration started successfully",
                    BatchSize = batchSize,
                    StartedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting migration");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error starting migration",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Migrate a specific folder
        /// </summary>
        /// <param name="folder">Folder name (drafts, pending, approved, archived)</param>
        /// <returns>Migration summary for the folder</returns>
        [HttpPost("folder/{folder}")]
        public async Task<IActionResult> MigrateFolder(string folder)
        {
            try
            {
                var validFolders = new[] { "drafts", "pending", "approved", "archived" };
                if (!validFolders.Contains(folder.ToLower()))
                {
                    return BadRequest($"Invalid folder. Valid folders are: {string.Join(", ", validFolders)}");
                }

                // Check if Google Drive is properly configured
                var hasValidTokens = await _googleDriveOAuthService.HasValidCompanyTokensAsync();
                if (!hasValidTokens)
                {
                    return BadRequest("Google Drive company account not authorized. Complete setup first.");
                }

                var summary = await _migrationService.MigrateFolderAsync(folder);

                return Ok(new
                {
                    Success = true,
                    Folder = folder,
                    Summary = summary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating folder {Folder}", folder);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Error migrating folder {folder}",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Migrate a specific document version
        /// </summary>
        /// <param name="documentVersionId">Document version ID to migrate</param>
        /// <returns>Migration result</returns>
        [HttpPost("document/{documentVersionId}")]
        public async Task<IActionResult> MigrateDocument(string documentVersionId)
        {
            try
            {
                // Check if Google Drive is properly configured
                var hasValidTokens = await _googleDriveOAuthService.HasValidCompanyTokensAsync();
                if (!hasValidTokens)
                {
                    return BadRequest("Google Drive company account not authorized. Complete setup first.");
                }

                var result = await _migrationService.MigrateFileAsync(documentVersionId);

                if (result.Success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = "Document migrated successfully",
                        Result = result
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = result.Message,
                        Error = result.ErrorDetails
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating document {DocumentVersionId}", documentVersionId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error migrating document",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Cancel ongoing migration
        /// </summary>
        /// <param name="jobId">Migration job ID</param>
        /// <returns>Cancellation result</returns>
        [HttpPost("cancel/{jobId}")]
        public async Task<IActionResult> CancelMigration(string jobId)
        {
            try
            {
                await _migrationService.CancelMigrationAsync(jobId);

                return Ok(new
                {
                    Success = true,
                    Message = "Migration cancelled successfully",
                    JobId = jobId,
                    CancelledAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling migration {JobId}", jobId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error cancelling migration",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Verify migration integrity for a specific document
        /// </summary>
        /// <param name="documentVersionId">Document version ID to verify</param>
        /// <returns>Verification result</returns>
        [HttpPost("verify/{documentVersionId}")]
        public async Task<IActionResult> VerifyMigration(string documentVersionId)
        {
            try
            {
                var result = await _migrationService.VerifyMigrationAsync(documentVersionId);

                return Ok(new
                {
                    Success = result.IsValid,
                    Message = result.Message,
                    Result = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying migration for document {DocumentVersionId}", documentVersionId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error verifying migration",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Rollback a migrated document back to Azure only
        /// </summary>
        /// <param name="documentVersionId">Document version ID to rollback</param>
        /// <returns>Rollback result</returns>
        [HttpPost("rollback/{documentVersionId}")]
        public async Task<IActionResult> RollbackDocument(string documentVersionId)
        {
            try
            {
                var result = await _migrationService.RollbackFileAsync(documentVersionId);

                if (result.Success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = "Document rolled back successfully",
                        Result = result
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = result.Message,
                        Error = result.ErrorDetails
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rolling back document {DocumentVersionId}", documentVersionId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error rolling back document",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get migration prerequisites and readiness check
        /// </summary>
        /// <returns>Migration readiness information</returns>
        [HttpGet("readiness")]
        public async Task<IActionResult> CheckMigrationReadiness()
        {
            try
            {
                var hasValidTokens = await _googleDriveOAuthService.HasValidCompanyTokensAsync();
                var migrationStatus = await _migrationService.GetMigrationStatusAsync();

                return Ok(new
                {
                    IsReady = hasValidTokens,
                    GoogleDriveConfigured = hasValidTokens,
                    CurrentMigrationStatus = migrationStatus.Status,
                    Prerequisites = new
                    {
                        CompanyAccountAuthorized = hasValidTokens,
                        GoogleDriveAPIEnabled = true, // Assume enabled if tokens exist
                        RedisConnected = true // Assume connected if we can check status
                    },
                    NextSteps = hasValidTokens
                        ? "Ready to start migration"
                        : "Complete Google Drive setup first using /api/googledrive-setup/company-auth-url"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking migration readiness");
                return StatusCode(500, "Error checking migration readiness");
            }
        }
    }
}
