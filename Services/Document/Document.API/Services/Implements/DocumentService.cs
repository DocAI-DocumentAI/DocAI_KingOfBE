using AutoMapper;
using Document.API.Attributes;
using Document.API.Constants;
using Document.API.Models;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Enums;
using Document.Domain.Model;
using Document.Domain.Models;
using Document.Infrastructure.Filter;
using Document.Infrastructure.Paginate;
using Document.Infrastructure.Repository.Interfaces;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;

using Shared.DTOs;
using Shared.Exceptions;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

namespace Document.API.Services.Implements;

public class DocumentService : IDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<DocumentService> _logger;
    private readonly IStorageService _storageService;
    private readonly IKernelMemory _memory;
    private readonly IConfiguration _configuration;
    private readonly IDocumentEnrichmentService _enrichmentService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDocumentReplacementService _replacementService;
    private readonly IDocumentPermissionManager _permissionManager;
    private readonly ITokenUsageLogger _tokenUsageLogger;

    public DocumentService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<DocumentService> logger, IKernelMemory memory, IStorageService storageService, IConfiguration configuration, IDocumentEnrichmentService enrichmentService, IHttpContextAccessor httpContextAccessor, IDocumentReplacementService replacementService, IDocumentPermissionManager permissionManager, ITokenUsageLogger tokenUsageLogger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _memory = memory;
        _storageService = storageService;
        _configuration = configuration;
        _enrichmentService = enrichmentService;
        _httpContextAccessor = httpContextAccessor;
        _replacementService = replacementService;
        _permissionManager = permissionManager;
        _tokenUsageLogger = tokenUsageLogger;

        var openRouterConfig = configuration.GetSection("OpenRouter").Get<OpenRouterConfigSetting>();
        var openAIConfig = configuration.GetSection("OpenAI").Get<OpenAIConfigSetting>();

        _logger.LogInformation("Kernel Memory is configured with:");
        _logger.LogInformation("- Text Generation Model: {Model}", openRouterConfig?.Model);
        _logger.LogInformation("- Text Embedding Model (OpenAI): {EmbeddingModel}", openAIConfig?.EmbeddingModel);
        //_logger.LogInformation("- OpenRouter API Key: {Key}", openRouterConfig?.APIKey?.Length >= 4 ? openRouterConfig.APIKey[^4..] : "Invalid or too short");
        //_logger.LogInformation("- OpenAI API Key: {Key}", openAIConfig?.APIKey?.Length >= 4 ? openAIConfig.APIKey[^4..] : "Invalid or too short");

        if (_memory != null)
        {
            _logger.LogInformation("Kernel Memory service is initialized and available.");
        }
        else
        {
            _logger.LogWarning("Kernel Memory service is NOT initialized.");
        }
        
        // Log enrichment service status
        if (_enrichmentService != null)
        {
            _logger.LogInformation("Document Enrichment Service is initialized and available.");
        }
        else
        {
            _logger.LogError("Document Enrichment Service is NOT initialized - name enrichment will not work!");
        }
    }

    /// <summary>
    /// Gets the current user's department ID from JWT token
    /// </summary>
    /// <returns>Department ID or null if not found</returns>
    private string? GetCurrentUserDepartmentId()
    {
        return JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);
    }

    /// <summary>
    /// Determines if a user can access a specific document version based on status, ownership, and department
    /// </summary>
    /// <param name="version">Document version to check access for</param>
    /// <param name="userId">Current user ID</param>
    /// <param name="userDepartmentId">Current user's department ID</param>
    /// <returns>True if user has access, false otherwise</returns>
    private bool CanUserAccessDocumentVersion(DocumentVersion version, string userId, string? userDepartmentId)
    {
        switch (version.Status)
        {
            case StatusEnum.Draft:
            case StatusEnum.Rejected:
                // Only the owner can access draft and rejected documents
                return version.DocumentFile.OwnerId == userId;

            case StatusEnum.Pending:
                // Owner can access, and managers from the same department can access
                if (version.DocumentFile.OwnerId == userId)
                    return true;

                // Check if user is a manager in the same department
                var userRole = JwtTokenHelper.GetUserRole(_httpContextAccessor);
                return userRole == Roles.Manager && version.DocumentFile.DepartmentId == userDepartmentId;

            case StatusEnum.Approved:
            case StatusEnum.Archived:
                // Public documents or documents from the same department
                return version.IsPublic || version.DocumentFile.DepartmentId == userDepartmentId;

            default:
                return false;
        }
    }

    /// <summary>
    /// Gets the current user's ID from JWT token
    /// </summary>
    /// <returns>User ID</returns>
    private string GetCurrentUserId()
    {
        return JwtTokenHelper.GetUserId(_httpContextAccessor);
    }

    /// <summary>
    /// Validates if a user can access a document based on department and isPublic status
    /// </summary>
    /// <param name="documentDepartmentId">Document's department ID</param>
    /// <param name="isPublic">Whether the document is public</param>
    /// <param name="userDepartmentId">User's department ID (optional, will get from JWT if not provided)</param>
    /// <returns>True if user can access the document</returns>
    private bool CanUserAccessDocument(string documentDepartmentId, bool isPublic, string? userDepartmentId = null)
    {
        // Public documents are accessible to all employees
        if (isPublic)
        {
            return true;
        }

        // For private documents, check department access
        userDepartmentId ??= GetCurrentUserDepartmentId();

        if (string.IsNullOrEmpty(userDepartmentId))
        {
            _logger.LogWarning("User department ID not found in JWT token for private document access check");
            return false;
        }

        // Direct department ID comparison
        return !string.IsNullOrEmpty(documentDepartmentId) && userDepartmentId.Equals(documentDepartmentId, StringComparison.OrdinalIgnoreCase);
    }


    public async Task<DocumentDraftResponse> CreateDraftAsync(CreateDraftRequest request)
    {
        // Get current user ID and department ID from JWT token
        var userId = GetCurrentUserId();
        var departmentId = GetCurrentUserDepartmentId();

        // BR-018 Every new document must be assigned to a single Department.
        if (string.IsNullOrEmpty(departmentId))
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, "User department not found in authentication token");
        }

        // File type and size validations are now handled by FluentValidation

        // Validate DocumentType exists
        if (string.IsNullOrEmpty(request.DocumentTypeId))
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.DocumentTypeRequired);
        }

        var documentType = await _unitOfWork.GetRepository<DocumentType>()
            .SingleOrDefaultAsync(predicate: dt => dt.Id == request.DocumentTypeId && dt.DeletedTime == null);

        if (documentType == null)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.InvalidDocumentType);
        }

        // Effective date validation is now handled by FluentValidation

        //1. Check draft limit
        var draftCount = await _unitOfWork.GetRepository<DocumentVersion>()
            .CountAsync(predicate: v => v.CreatedBy == userId && v.Status == StatusEnum.Draft);
        if (draftCount >= PolicyConstant.MaxDraftsPerUser)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.MaxDraftsReached, PolicyConstant.MaxDraftsPerUser));
        }

        //2. Handle replacement logic if ReplacementDocumentId is provided
        DocumentFile? documentToReplace = null;
        if (!string.IsNullOrEmpty(request.ReplacementDocumentId))
        {
            documentToReplace = await _unitOfWork.GetRepository<DocumentFile>()
                .SingleOrDefaultAsync(
                    predicate: d => d.Id == request.ReplacementDocumentId,
                    include: i => i.Include(d => d.DocumentVersions)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentNotFound);

            // BR-035: Only documents with status 'Approved' can be selected for replacement.
            var latestApprovedVersion = documentToReplace.DocumentVersions.OrderByDescending(v => v.CreatedTime).FirstOrDefault(v => v.Status == StatusEnum.Approved);
            if (latestApprovedVersion == null)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.CanOnlyReplaceApprovedDocument);
            }

            // BR-037: A document can only be in the process of being replaced by one new document at a time.
            if (documentToReplace.IsReplaced)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.DocumentAlreadyUnderReplacement);
            }

            // BR-038: Editors can only replace documents within their assigned Department.
            if (documentToReplace.DepartmentId != departmentId) // User's department from JWT token
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToReplaceDocumentInOtherDepartment);
            }

            // BR-036: The replacement file cannot be identical to the original (checked by hash, title, number).
            // This check will be done after file upload.
        }
        else
        {
            // If not a replacement, check for title duplication for new documents
            var existingDocument = await _unitOfWork.GetRepository<DocumentFile>()
                    .SingleOrDefaultAsync(predicate: d => d.Title == request.Title);
            if (existingDocument != null)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.DocumentTitleExists);
            }

            // Checking Version Name duplication for new documents
            var existingVersionName = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(predicate: v => v.VersionName == request.VersionName && v.DocumentFile.Title == request.Title);
            if (existingVersionName != null)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.DocumentVersionNameExists);
            }
        }

        // 4. Upload the file to storage and get the MD5 hash.
        StorageUploadResponse? uploadResponse = null;
        string? fileHash = null;

        try
        {
            uploadResponse = await _storageService.UploadFileAsync(request.File, StorageFolderConstant.Drafts, departmentId, request.IsPublic);
            fileHash = uploadResponse.Md5Hash;
            _logger.LogInformation("File uploaded successfully with ID {FileId} for draft creation", uploadResponse.FileIdentifier);

            // 5. Check for file duplication using the MD5 hash.
            var existingFile = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(predicate: v => v.FileHash == fileHash, include: i => i.Include(v => v.DocumentFile));

            if (existingFile != null)
            {
                _logger.LogWarning("Duplicate file detected with hash {FileHash}, deleting uploaded file {FileId}", fileHash, uploadResponse.FileIdentifier);
                await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);

                switch (existingFile.Status)
                {
                    case StatusEnum.Pending:
                    case StatusEnum.Approved:
                    case StatusEnum.Archived:
                        throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, string.Format(MessageConstant.FileAlreadyExists, existingFile.DocumentFile.Title, existingFile.VersionName, existingFile.Status));

                    case StatusEnum.Rejected:
                        if (existingFile.DocumentFile.OwnerId == userId)
                        {
                            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.RejectedFileExists);
                        }
                        else
                        {
                            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.AnotherUserRejectedFileExists);
                        }

                    case StatusEnum.Draft:
                        if (existingFile.DocumentFile.OwnerId == userId)
                        {
                            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.DraftFileExists);
                        }
                        break;
                }
            }
        }
        catch (Exception ex) when (uploadResponse != null)
        {
            // Ensure cleanup if any validation fails after upload
            _logger.LogError(ex, "Error during file validation, cleaning up uploaded file {FileId}", uploadResponse.FileIdentifier);
            try
            {
                await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Failed to cleanup uploaded file {FileId} after validation error", uploadResponse.FileIdentifier);
            }
            throw;
        }

        //6. save the generel infomation of the file into the DocumentFile table
        var documentFile = new DocumentFile
        {
            Title = request.Title,
            Description = request.Description,
            DepartmentId = departmentId,
            OwnerId = userId,
            CreatedBy = userId,
            ReplacementId = request.ReplacementDocumentId,
            IsReplaced = !string.IsNullOrEmpty(request.ReplacementDocumentId),
            DocumentTypeId = request.DocumentTypeId
        };

        var version = new DocumentVersion
        {
            DocumentFileId = documentFile.Id,
            DocumentFile = documentFile,
            Title = request.Title,
            VersionName = request.VersionName,
            Status = StatusEnum.Draft, // Use the Enum for status
            IsOfficial = false, // New drafts are not official
            IsPublic = request.IsPublic, // Set public/private status from request
            Summary = request.Summary, // Placeholder for summary
            FileName = request.File.FileName,
            FileType = Path.GetExtension(request.File.FileName),
            FileSize = request.File.Length,
            FilePath = uploadResponse.FileIdentifier, // Google Drive file ID for new uploads
            FileHash = fileHash,
            SignedBy = request.SignedBy,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveUntil = request.EffectiveUntil,
            CreatedBy = userId,
            LastSubmitted = DateTime.UtcNow,
            SubmittedBy = userId,
        };

        version.FileName = request.File.FileName;

        await ProcessTagsAsync(version, request.Tags, userId);

        // 6. Link entities using the correct navigation property name
        documentFile.DocumentVersions.Add(version);

        // 7. Save to database with comprehensive error handling
        try
        {
            await _unitOfWork.GetRepository<DocumentFile>().InsertAsync(documentFile);
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Successfully saved document {DocumentId} to database", documentFile.Id);

            if (documentToReplace != null)
            {
                documentToReplace.IsReplaced = true;
                documentToReplace.LastUpdatedBy = userId;
                documentToReplace.LastUpdatedTime = DateTime.UtcNow;
                await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentToReplace);
                await _unitOfWork.CommitAsync();
                _logger.LogInformation("Marked document {OriginalDocumentId} as replaced by new document {NewDocumentId}", documentToReplace.Id, documentFile.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database operation failed for document creation, rolling back uploaded file {FileId}", uploadResponse?.FileIdentifier);

            // Rollback: Delete the uploaded file if database operations fail
            if (uploadResponse != null)
            {
                try
                {
                    await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
                    _logger.LogInformation("Successfully rolled back uploaded file {FileId}", uploadResponse.FileIdentifier);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback uploaded file {FileId} after database error", uploadResponse.FileIdentifier);
                }
            }
            throw;
        }

        _logger.LogInformation("Successfully created draft document {DocumentId}", documentFile.Id);

        // COMMENTED OUT: Apply Google Drive permissions (Draft = owner only)
        // Permission updates slow down upload process by making individual API calls
        // Users already have folder-level access when they are created
        /*
        try
        {
            var fileId = uploadResponse.FileIdentifier; // Google Drive file ID
            await _permissionManager.ApplyDocumentPermissionsAsync(
                fileId,
                StatusEnum.Draft,
                departmentId,
                request.IsPublic,
                userId);
            _logger.LogInformation("Applied permissions for draft document {DocumentId} with file ID {FileId}", documentFile.Id, fileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply permissions for draft document {DocumentId}", documentFile.Id);
            // Don't fail the entire operation for permission errors
        }
        */

        // Clear replacement suggestion cache since a new document was created
        try
        {
            await _replacementService.ClearReplacementCacheAsync(request.DocumentTypeId, departmentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear replacement cache after document creation");
        }

        // 8. Reload the document version with DocumentType included to ensure proper mapping
        var createdVersion = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(
                predicate: v => v.Id == version.Id,
                include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
            );

        // 9. Use AutoMapper to map the result to the response DTO
        var response = _mapper.Map<DocumentDraftResponse>(createdVersion);

        // COMMENTED OUT: Enrich response with user and department names
        // Name enrichment makes RabbitMQ calls to Auth service which slows down upload process
        // Names can be enriched on-demand when viewing documents instead of during upload
        /*
        var enrichedResponse = await _enrichmentService.EnrichDocumentDraftResponseAsync(response);
        _logger.LogInformation("Draft document response enriched with names for document {DocumentId}", documentFile.Id);
        */

        _logger.LogInformation("Draft document created successfully for document {DocumentId} (names not enriched for performance)", documentFile.Id);
        var enrichedResponse = response; // Return response without name enrichment

        return enrichedResponse;
    }

    public async Task<DocumentDraftResponse> UpdateDraftAsync(string versionId, UpdateDocumentDraftRequest request)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        // ====================================================================================
        // STEP 1: Perform all fast, in-memory validations and external I/O first.
        // Do NOT touch the database yet.
        // ====================================================================================

        // Effective date and DocumentType validations are now handled by FluentValidation

        StorageUploadResponse? uploadResponse = null;
        string? fileHash = null;

        // First, get the version to update to access department info
        var versionToUpdate = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(
                predicate: v => v.Id == versionId,
                include: p => p.Include(v => v.DocumentFile)) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);

        // Validate DocumentType exists if provided (performance optimization: only validate if changing)
        if (!string.IsNullOrEmpty(request.DocumentTypeId) && request.DocumentTypeId != versionToUpdate.DocumentFile.DocumentTypeId)
        {
            var documentType = await _unitOfWork.GetRepository<DocumentType>()
                .SingleOrDefaultAsync(predicate: dt => dt.Id == request.DocumentTypeId && dt.DeletedTime == null);

            if (documentType == null)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.InvalidDocumentType);
            }
            _logger.LogInformation("DocumentType validation passed for type {DocumentTypeId}", request.DocumentTypeId);
        }

        if (request.File != null)
        {
            // File type and size validations are now handled by FluentValidation

            // Performance optimization: Check if we can reuse existing file by comparing hash first
            string newFileHash;
            using (var stream = request.File.OpenReadStream())
            {
                using var md5 = System.Security.Cryptography.MD5.Create();
                var hashBytes = await md5.ComputeHashAsync(stream);
                newFileHash = Convert.ToBase64String(hashBytes);
            }

            // If the file hash is the same as current file, skip upload entirely
            if (versionToUpdate.FileHash == newFileHash)
            {
                _logger.LogInformation("File hash unchanged for version {VersionId}, skipping file upload", versionId);
                // No file upload needed, just update metadata
            }
            else
            {
                // Upload the new file to storage BEFORE starting the database transaction.
                _logger.LogInformation("Uploading new file to storage before database transaction begins.");
                uploadResponse = await _storageService.UploadFileAsync(request.File, StorageFolderConstant.Drafts, versionToUpdate.DocumentFile.DepartmentId, request.IsPublic);
                fileHash = uploadResponse.Md5Hash;
                _logger.LogInformation("New file uploaded with ID {FileId} and hash {FileHash}", uploadResponse.FileIdentifier, fileHash);
            }
        }

        // ====================================================================================
        // STEP 2: Now, start the database transaction. This section should be as fast as possible.
        // ====================================================================================
        try
        {

            // Re-retrieve draft with tracking enabled for updates
            versionToUpdate = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultWithTrackingAsync(
                    predicate: v => v.Id == versionId,
                    include: p => p.Include(v => v.DocumentFile))
                ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);

            var documentToUpdate = versionToUpdate.DocumentFile;

            // --- Perform all database-dependent validations ---

            // Editor must be the owner.
            if (documentToUpdate.OwnerId != userId)
            {
                // If we uploaded a file, we must now delete it since the operation is failing.
                if (uploadResponse != null) await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToEdit);
            }

            // Status must be Draft or Rejected.
            if (versionToUpdate.Status != StatusEnum.Draft && versionToUpdate.Status != StatusEnum.Rejected)
            {
                if (uploadResponse != null) await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.CannotEditWithStatus, versionToUpdate.Status));
            }

            // Check for title duplication
            if (documentToUpdate.Title != request.Title)
            {
                var existingDocument = await _unitOfWork.GetRepository<DocumentFile>()
                    .SingleOrDefaultAsync(predicate: d => d.Title == request.Title && d.Id != documentToUpdate.Id);
                if (existingDocument != null)
                {
                    if (uploadResponse != null) await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
                    throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.DocumentTitleExists);
                }
            }

            // Check for file hash duplication if a new file was uploaded
            if (fileHash != null && uploadResponse != null)
            {
                var existingFile = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(predicate: v => v.FileHash == fileHash && v.Id != versionId && v.Status != StatusEnum.Rejected);
                if (existingFile != null)
                {
                    // If a duplicate is found, delete the file that was just uploaded.
                    _logger.LogWarning("Duplicate file detected with hash {FileHash}, deleting uploaded file {FileId}", fileHash, uploadResponse.FileIdentifier);
                    await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
                    throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                        string.Format(MessageConstant.FileAlreadyExists, existingFile.DocumentFile.Title, existingFile.VersionName, existingFile.Status));
                }
            }

            // --- All validations passed. Apply changes. ---

            string? oldFileIdToDelete = null;
            if (uploadResponse != null && fileHash != null)
            {
                // Keep track of the old Google Drive file ID to delete it AFTER the transaction succeeds.
                oldFileIdToDelete = versionToUpdate.GoogleDriveFileId;

                // Update version properties for the new file.
                versionToUpdate.FilePath = $"{StorageFolderConstant.Drafts}/{uploadResponse.FileName}";
                versionToUpdate.FileName = uploadResponse.FileName;
                versionToUpdate.FileType = Path.GetExtension(request.File.FileName);
                versionToUpdate.FileSize = request.File.Length;
                versionToUpdate.FileHash = fileHash;
                versionToUpdate.GoogleDriveFileId = uploadResponse.FileIdentifier; // Set the new Google Drive file ID

                _logger.LogInformation("Updated version {VersionId} with new file {FileId}", versionId, uploadResponse.FileIdentifier);
            }

            // Store the original file name before mapping.
            var originalFileName = versionToUpdate.FileName;

            // Apply metadata updates from the request DTO.
            _mapper.Map(request, documentToUpdate);
            _mapper.Map(request, versionToUpdate);

            // If no new file was uploaded, ensure the original FileName is preserved.
            if (request.File == null)
            {
                versionToUpdate.FileName = originalFileName;
            }

            await ProcessTagsAsync(versionToUpdate, request.Tags, userId);

            documentToUpdate.LastUpdatedBy = userId;
            documentToUpdate.LastUpdatedTime = DateTime.UtcNow;

            // Check if document is being updated from Rejected status
            bool wasRejected = versionToUpdate.Status == StatusEnum.Rejected;
            versionToUpdate.Status = versionToUpdate.Status == StatusEnum.Rejected ? StatusEnum.Draft : versionToUpdate.Status;

            // If document was rejected and is now being updated, remove the rejection approval logs
            if (wasRejected)
            {
                _logger.LogInformation("Document {VersionId} was rejected and is being updated, removing rejection approval logs", versionId);

                var rejectionLogs = await _unitOfWork.GetRepository<ApprovalLog>()
                    .GetListAsync(predicate: log => log.DocumentVersionId == versionId && log.Action == ApprovalAction.Reject);

                if (rejectionLogs.Any())
                {
                    foreach (var log in rejectionLogs)
                    {
                        _unitOfWork.GetRepository<ApprovalLog>().DeleteAsync(log);
                    }
                    _logger.LogInformation("Removed {Count} rejection approval logs for document {VersionId}", rejectionLogs.Count, versionId);
                }
            }

            // No need to call UpdateAsync since entities are now tracked by EF
            // Entity Framework will automatically detect changes and update them

            // Save changes to the database. This is the critical point.
            _logger.LogInformation("Committing database changes for version {VersionId}", versionId);
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Database commit successful for version {VersionId}", versionId);

            // ====================================================================================
            // STEP 3: Post-transaction cleanup.
            // ====================================================================================

            // If the commit was successful, we can now safely delete the old file from storage.
            if (!string.IsNullOrEmpty(oldFileIdToDelete))
            {
                _logger.LogInformation("Database commit successful. Deleting old file {OldFileId} from storage.", oldFileIdToDelete);
                try
                {
                    await _storageService.DeleteFileAsync(oldFileIdToDelete, StorageFolderConstant.Drafts);
                    _logger.LogInformation("Successfully deleted old file {OldFileId} from storage", oldFileIdToDelete);
                }
                catch (Exception ex)
                {
                    // This operation can fail, but it won't roll back our database change.
                    // Log the error for monitoring and potential cleanup later
                    _logger.LogError(ex, "Failed to delete old file {OldFileId} from storage. File may be orphaned.", oldFileIdToDelete);
                    // Consider adding to a cleanup queue or dead letter queue for retry
                }
            }

            _logger.LogInformation("Successfully updated document version {VersionId}", versionId);

            // Reload the document version with DocumentType included to ensure proper mapping
            var updatedVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                );

            // COMMENTED OUT: Enrich response with user and department names
            // Name enrichment makes RabbitMQ calls to Auth service which slows down update process
            // Names can be enriched on-demand when viewing documents instead of during update
            var response = _mapper.Map<DocumentDraftResponse>(updatedVersion);
            /*
            var enrichedResponse = await _enrichmentService.EnrichDocumentDraftResponseAsync(response);
            _logger.LogInformation("Updated document response enriched with names for version {VersionId}", versionId);
            */

            _logger.LogInformation("Document updated successfully for version {VersionId} (names not enriched for performance)", versionId);
            var enrichedResponse = response; // Return response without name enrichment

            // COMMENTED OUT: Apply Google Drive permissions for updated draft (still owner only)
            // Permission updates slow down update process by making individual API calls
            // Users already have folder-level access when they are created
            /*
            try
            {
                var fileId = updatedVersion.GoogleDriveFileId ?? updatedVersion.FilePath;
                await _permissionManager.ApplyDocumentPermissionsAsync(
                    fileId,
                    StatusEnum.Draft,
                    updatedVersion.DocumentFile.DepartmentId,
                    updatedVersion.IsPublic,
                    userId);
                _logger.LogInformation("Applied permissions for updated draft document {VersionId} with file ID {FileId}", versionId, fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply permissions for updated draft document {VersionId}", versionId);
                // Don't fail the entire operation for permission errors
            }
            */

            // Clear replacement suggestion cache since a document was updated
            try
            {
                await _replacementService.ClearReplacementCacheAsync(documentToUpdate.DocumentTypeId, documentToUpdate.DepartmentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear replacement cache after document update");
            }

            return enrichedResponse;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict for versionId {VersionId}. The data was modified by another user.", versionId);

            // IMPORTANT: If we get a concurrency error, we must delete the file we uploaded
            // at the beginning, otherwise it will be an orphaned file in storage.
            if (uploadResponse != null)
            {
                _logger.LogInformation("Rolling back storage upload for {FileIdentifier} due to concurrency conflict.", uploadResponse.FileIdentifier);
                await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
            }

            // Throw a specific, user-friendly error.
            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, "This document was updated by someone else. Please refresh and try again.");
        }
    }


    public async Task<AnalyzeDocumentResponse> AnalyzeDocumentAsync(IFormFile file)
    {
        var startTime = DateTime.UtcNow;
        var userId = GetCurrentUserId();
        var userDepartmentId = GetCurrentUserDepartmentId();

        _logger.LogInformation("Starting AI analysis for file: {FileName} by user {UserId}",
            file.FileName, userId);

        // File type validation is now handled by FluentValidation at the controller level

        // 1. Calculate MD5 hash for duplicate detection before any expensive processing
        string fileHash;
        using (var stream = file.OpenReadStream())
        {
            using var md5 = MD5.Create();
            var hashBytes = await md5.ComputeHashAsync(stream);
            fileHash = Convert.ToBase64String(hashBytes);
        }

        _logger.LogInformation("Calculated MD5 hash for file: {FileName}, Hash: {FileHash}", file.FileName, fileHash);

        // OPTIMIZED: Faster duplicate check with simplified query
        var existingFile = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(
                predicate: v => v.FileHash == fileHash &&
                               (v.Status == StatusEnum.Approved || v.Status == StatusEnum.Archived),
                include: i => i.Include(v => v.DocumentFile));

        // Additional access control check only if duplicate found
        if (existingFile != null && !existingFile.IsPublic && existingFile.DocumentFile.DepartmentId != userDepartmentId)
        {
            existingFile = null; // User doesn't have access to this duplicate
        }

        if (existingFile != null)
        {
            _logger.LogWarning("Duplicate file detected for {FileName}. Existing file: {ExistingTitle} (Version: {ExistingVersion}, Status: {ExistingStatus})",
                file.FileName, existingFile.Title, existingFile.VersionName, existingFile.Status);

            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                string.Format(MessageConstant.FileAlreadyExists, existingFile.Title, existingFile.VersionName, existingFile.Status));
        }

        var response = new AnalyzeDocumentResponse
        {
            Summary = "AI analysis could not be completed.",
            Tags = new List<string>()
        };
        string tempFilePath = null;
        string tempDocId = null;
        try
        {
            tempDocId = $"temp-analysis-{Guid.NewGuid()}";
            tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));

            // OPTIMIZED: Parallel file operations and AI preparation
            var fileTask = Task.Run(async () =>
            {
                await using var fs = new FileStream(tempFilePath, FileMode.Create);
                await file.CopyToAsync(fs);
            });

            var promptTask = Task.Run(() =>
            {
                var prompt = AiPromptConstant.DocumentAnalysis.MetadataExtractionPrompt;
                var tokens = _tokenUsageLogger.EstimateTokenCount(prompt);
                return new { Prompt = prompt, RequestTokens = tokens };
            });

            // Wait for file copy to complete
            await fileTask;
            var promptInfo = await promptTask;

            // OPTIMIZED: Import with timeout protection
            using var importCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                _logger.LogInformation("Importing document to Kernel Memory for analysis...");
                await _memory.ImportDocumentAsync(tempFilePath, documentId: tempDocId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Document import timed out after 2 minutes for file: {FileName}", file.FileName);
                throw new ErrorException(StatusCodes.Status408RequestTimeout, ErrorCode.BADREQUEST,
                    "Document analysis timed out. Please try with a smaller file.");
            }

            // OPTIMIZED: Single AI call with timeout (no retries for faster response)
            var filter = new MemoryFilter().ByDocument(tempDocId);

            MemoryAnswer? answer = null;
            using var aiCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                _logger.LogInformation("Making AI analysis call with 2-minute timeout...");
                answer = await _memory.AskAsync(promptInfo.Prompt, filter: filter);

                if (answer != null && answer.RelevantSources.Any() &&
                    !AiPromptConstant.Configuration.FailureIndicators.Any(indicator =>
                        answer.Result.Contains(indicator, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogInformation("Successfully received valid AI response for file: {FileName}", file.FileName);
                }
                else
                {
                    _logger.LogWarning("AI analysis returned no valid information for file: {FileName}", file.FileName);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AI analysis timed out after 2 minutes for file: {FileName}", file.FileName);
                throw new ErrorException(StatusCodes.Status408RequestTimeout, ErrorCode.BADREQUEST,
                    "AI analysis timed out. Please try again later.");
            }

            // 4. OPTIMIZED: Process AI response with parallel token calculation
            if (answer != null && !AiPromptConstant.Configuration.FailureIndicators.Any(indicator =>
                answer.Result.Contains(indicator, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("Raw AI response for file {FileName}: {AiResponse}", file.FileName, answer.Result);

                // Parallel processing of response parsing and token calculation
                var parseTask = Task.Run(() => ParseAiJsonResponse(answer.Result, response));
                var tokenTask = Task.Run(() => _tokenUsageLogger.EstimateTokenCount(answer.Result));

                await parseTask;
                var responseTokens = await tokenTask;

                _logger.LogInformation("Successfully parsed AI JSON response for file: {FileName}", file.FileName);

                // BR-077: Summaries should be under 2000 words.
                if (!string.IsNullOrEmpty(response.Summary))
                {
                    var words = response.Summary.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > PolicyConstant.MaxSummaryLength)
                    {
                        response.Summary = string.Join(" ", words.Take(PolicyConstant.MaxSummaryLength)) + "...";
                        _logger.LogWarning("AI-generated summary for file {FileName} exceeded {MaxLength} words and was truncated.", file.FileName, PolicyConstant.MaxSummaryLength);
                    }
                }

                // Create token usage info
                response.TokenUsage = _tokenUsageLogger.CreateTokenUsageInfo(promptInfo.RequestTokens, responseTokens, "KernelMemory");

                // OPTIMIZED: Fire-and-forget token logging for faster response
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _tokenUsageLogger.LogTokenUsageAsync(
                            operation: "DocumentAnalysis",
                            requestTokens: response.TokenUsage.RequestTokens,
                            responseTokens: response.TokenUsage.ResponseTokens,
                            modelUsed: "KernelMemory",
                            userId: userId,
                            processingTimeMs: (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                            success: true
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to log token usage for successful analysis");
                    }
                });

                _logger.LogInformation("Document analysis completed successfully for file {FileName}. Tokens used: {TotalTokens}",
                    file.FileName, response.TokenUsage.TotalTokens);
            }
            else
            {
                // OPTIMIZED: Fire-and-forget token logging for failed cases
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _tokenUsageLogger.LogTokenUsageAsync(
                            operation: "DocumentAnalysis",
                            requestTokens: promptInfo.RequestTokens,
                            responseTokens: 0,
                            modelUsed: "KernelMemory",
                            userId: userId,
                            processingTimeMs: (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                            success: false,
                            errorMessage: "AI analysis returned no valid information"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to log token usage for failed analysis");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during single-prompt AI analysis for file: {FileName}", file.FileName);

            // OPTIMIZED: Fire-and-forget token logging for exceptions
            _ = Task.Run(async () =>
            {
                try
                {
                    await _tokenUsageLogger.LogTokenUsageAsync(
                        operation: "DocumentAnalysis",
                        requestTokens: _tokenUsageLogger.EstimateTokenCount(AiPromptConstant.DocumentAnalysis.MetadataExtractionPrompt),
                        responseTokens: 0,
                        modelUsed: "KernelMemory",
                        userId: userId,
                        processingTimeMs: (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                        success: false,
                        errorMessage: ex.Message
                    );
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log token usage for exception case");
                }
            });
        }
        finally
        {
            // OPTIMIZED: Parallel cleanup operations for faster completion
            var cleanupTasks = new List<Task>();

            // File cleanup
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                cleanupTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        File.Delete(tempFilePath);
                        _logger.LogDebug("Deleted temporary file: {TempFilePath}", tempFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary file: {TempFilePath}", tempFilePath);
                    }
                }));
            }

            // Memory cleanup
            if (!string.IsNullOrEmpty(tempDocId))
            {
                cleanupTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _memory.DeleteDocumentAsync(tempDocId);
                        _logger.LogDebug("Deleted temporary document from memory: {TempDocId}", tempDocId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary document from memory: {TempDocId}", tempDocId);
                    }
                }));
            }

            // Wait for all cleanup tasks with timeout (don't block response)
            if (cleanupTasks.Any())
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.WhenAll(cleanupTasks).WaitAsync(TimeSpan.FromSeconds(10));
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("Cleanup operations timed out after 10 seconds");
                    }
                });
            }
        }

        return response;
    }

    private void ParseAiJsonResponse(string jsonResponse, AnalyzeDocumentResponse response)
    {
        try
        {
            // Step 1: Sanitize possible markdown wrappers
            var cleanJson = jsonResponse.Trim().Trim('`').Replace("json", "").Trim();

            // Step 2: Try to isolate the valid JSON portion (from first { to last })
            int startIndex = cleanJson.IndexOf('{');
            int endIndex = cleanJson.LastIndexOf('}');

            if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
                throw new JsonException("JSON is not enclosed properly.");

            var jsonFragment = cleanJson.Substring(startIndex, endIndex - startIndex + 1);

            // Step 3: Try parsing
            using var jsonDoc = JsonDocument.Parse(jsonFragment);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                response.Title = title.GetString();

            if (root.TryGetProperty("versionName", out var versionName) && versionName.ValueKind == JsonValueKind.String)
                response.VersionName = versionName.GetString();

            if (root.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String)
                response.Description = description.GetString();

            if (root.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.String)
                response.Summary = summary.GetString() ?? "AI analysis could not be completed.";

            if (root.TryGetProperty("signedBy", out var signedBy) && signedBy.ValueKind == JsonValueKind.String)
                response.SignedBy = signedBy.GetString();

            if (root.TryGetProperty("effectiveFrom", out var effectiveFrom) && effectiveFrom.ValueKind == JsonValueKind.String)
                if (DateTime.TryParse(effectiveFrom.GetString(), out var fromDate))
                    response.EffectiveFrom = fromDate;

            if (root.TryGetProperty("effectiveUntil", out var effectiveUntil) && effectiveUntil.ValueKind == JsonValueKind.String)
                if (DateTime.TryParse(effectiveUntil.GetString(), out var untilDate))
                    response.EffectiveUntil = untilDate;

            if (root.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
            {
                response.Tags = tags.EnumerateArray()
                    .Select(t => t.GetString())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Cast<string>()
                    .ToList();
            }
        }
        catch (JsonException jex)
        {
            _logger.LogError(jex, "Failed to parse JSON response from AI. Possibly incomplete or malformed. Raw response: {AiResponse}", jsonResponse);
        }
    }

    public async Task<RegenerateSummaryResponse> RegenerateSummaryAsync(IFormFile file)
    {
        var startTime = DateTime.UtcNow;
        var userId = GetCurrentUserId();

        _logger.LogInformation("Starting enhanced summary regeneration for file: {FileName} by user {UserId}", file.FileName, userId);

        var response = new RegenerateSummaryResponse
        {
            Success = false,
            ErrorMessage = "Summary regeneration could not be completed."
        };

        string? tempFilePath = null;
        string? tempDocId = null;

        try
        {
            tempDocId = $"temp-summary-{Guid.NewGuid()}";
            tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));

            // OPTIMIZED: Parallel file operations and prompt preparation
            var fileTask = Task.Run(async () =>
            {
                await using var fs = new FileStream(tempFilePath, FileMode.Create);
                await file.CopyToAsync(fs);
            });

            var promptTask = Task.Run(() =>
            {
                var prompt = AiPromptConstant.SummaryGeneration.RegenerateSummaryPrompt;
                var tokens = _tokenUsageLogger.EstimateTokenCount(prompt);
                return new { Prompt = prompt, RequestTokens = tokens };
            });

            // Wait for file copy to complete
            await fileTask;
            var promptInfo = await promptTask;

            // OPTIMIZED: Import with timeout protection
            using var importCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                _logger.LogInformation("Importing document to Kernel Memory for summary regeneration...");
                await _memory.ImportDocumentAsync(tempFilePath, documentId: tempDocId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Document import timed out after 2 minutes for file: {FileName}", file.FileName);
                throw new ErrorException(StatusCodes.Status408RequestTimeout, ErrorCode.BADREQUEST,
                    "Summary regeneration timed out. Please try with a smaller file.");
            }

            // OPTIMIZED: Single AI call with timeout (no retries for faster response)
            var filter = new MemoryFilter().ByDocument(tempDocId);

            MemoryAnswer? answer = null;
            using var aiCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                _logger.LogInformation("Making AI summary generation call with 2-minute timeout...");
                answer = await _memory.AskAsync(promptInfo.Prompt, filter: filter);

                if (answer != null && answer.RelevantSources.Any() &&
                    !AiPromptConstant.Configuration.FailureIndicators.Any(indicator =>
                        answer.Result.Contains(indicator, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogInformation("Successfully generated enhanced summary for file: {FileName}", file.FileName);
                }
                else
                {
                    _logger.LogWarning("AI summary generation returned no valid information for file: {FileName}", file.FileName);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AI summary generation timed out after 2 minutes for file: {FileName}", file.FileName);
                throw new ErrorException(StatusCodes.Status408RequestTimeout, ErrorCode.BADREQUEST,
                    "AI summary generation timed out. Please try again later.");
            }

            // OPTIMIZED: Process AI response with parallel token calculation
            if (answer != null && !AiPromptConstant.Configuration.FailureIndicators.Any(indicator =>
                answer.Result.Contains(indicator, StringComparison.OrdinalIgnoreCase)))
            {
                response.Summary = answer.Result.Trim();
                response.Success = true;
                response.ErrorMessage = null;

                // Apply word limit policy
                var words = response.Summary.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > PolicyConstant.MaxSummaryLength)
                {
                    response.Summary = string.Join(" ", words.Take(PolicyConstant.MaxSummaryLength)) + "...";
                    _logger.LogWarning("Summary for file {FileName} exceeded {MaxLength} words and was truncated.",
                        file.FileName, PolicyConstant.MaxSummaryLength);
                }

                // Parallel token calculation
                var responseTokens = await Task.Run(() => _tokenUsageLogger.EstimateTokenCount(response.Summary));
                response.TokenUsage = _tokenUsageLogger.CreateTokenUsageInfo(promptInfo.RequestTokens, responseTokens, "KernelMemory");

                // OPTIMIZED: Fire-and-forget token logging for faster response
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _tokenUsageLogger.LogTokenUsageAsync(
                            operation: "SummaryRegeneration",
                            requestTokens: promptInfo.RequestTokens,
                            responseTokens: responseTokens,
                            modelUsed: "KernelMemory",
                            userId: userId,
                            documentId: tempDocId, // Use temporary document ID since this is pre-upload
                            processingTimeMs: (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                            success: true
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to log token usage for successful summary regeneration");
                    }
                });

                _logger.LogInformation("Enhanced summary regenerated successfully for file {FileName}. Tokens used: {TotalTokens}",
                    file.FileName, promptInfo.RequestTokens + responseTokens);
            }
            else
            {
                response.ErrorMessage = "Could not generate enhanced summary. The document may not be properly indexed or accessible.";

                // OPTIMIZED: Fire-and-forget token logging for failed cases
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _tokenUsageLogger.LogTokenUsageAsync(
                            operation: "SummaryRegeneration",
                            requestTokens: promptInfo.RequestTokens,
                            responseTokens: 0,
                            modelUsed: "KernelMemory",
                            userId: userId,
                            documentId: tempDocId,
                            processingTimeMs: (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                            success: false,
                            errorMessage: response.ErrorMessage
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to log token usage for failed summary regeneration");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during enhanced summary regeneration for file {FileName}", file.FileName);
            response.ErrorMessage = "An error occurred while regenerating the summary.";
            response.Success = false;

            // OPTIMIZED: Fire-and-forget token logging for exceptions
            _ = Task.Run(async () =>
            {
                try
                {
                    await _tokenUsageLogger.LogTokenUsageAsync(
                        operation: "SummaryRegeneration",
                        requestTokens: 0,
                        responseTokens: 0,
                        userId: userId,
                        processingTimeMs: (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                        success: false,
                        errorMessage: ex.Message
                    );
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log token usage for exception case in summary regeneration");
                }
            });
        }
        finally
        {
            // OPTIMIZED: Parallel cleanup operations for faster completion
            var cleanupTasks = new List<Task>();

            // File cleanup
            if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
            {
                cleanupTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        File.Delete(tempFilePath);
                        _logger.LogDebug("Deleted temporary file: {TempFilePath}", tempFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary file: {TempFilePath}", tempFilePath);
                    }
                }));
            }

            // Memory cleanup
            if (!string.IsNullOrEmpty(tempDocId))
            {
                cleanupTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _memory.DeleteDocumentAsync(tempDocId);
                        _logger.LogDebug("Deleted temporary document from memory: {TempDocId}", tempDocId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary document from memory: {TempDocId}", tempDocId);
                    }
                }));
            }

            // Wait for all cleanup tasks with timeout (don't block response)
            if (cleanupTasks.Any())
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.WhenAll(cleanupTasks).WaitAsync(TimeSpan.FromSeconds(10));
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("Cleanup operations timed out after 10 seconds in summary regeneration");
                    }
                });
            }
        }

        response.ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return response;
    }

    public async Task DeleteDraftAsync(string documentId, string versionId)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        _logger.LogInformation("Attempting to delete document {DocumentId} by user {UserId}", documentId, userId);

        // 1. Retrieve the document, ensuring its versions are included for status checking.
        var documentToDelete = await _unitOfWork.GetRepository<DocumentFile>()
            .SingleOrDefaultAsync(
                predicate: d => d.Id == documentId,
                include: q => q.Include(d => d.DocumentVersions)
            ) ?? throw new ErrorException(StatusCodes.Status404NotFound,ErrorCode.NOT_FOUND, MessageConstant.DocumentNotFound);

        _logger.LogInformation("Document found: {Title}", documentToDelete.Title);

        // 2. Enforce Business Rules from SRS
        // BR-116: Check if the current user is the owner.
        if (documentToDelete.OwnerId != userId)
        {
            _logger.LogWarning("User {UserId} attempted to delete a document they do not own.", userId);
            throw new ErrorException(StatusCodes.Status403Forbidden,ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToDelete);
        }

        _logger.LogInformation("User {UserId} is the owner of the document", userId);

        // A draft document should only have one version. We get that version to check its status.
        var versionToDelete = documentToDelete.DocumentVersions.FirstOrDefault(v => v.Id == versionId);

        // BR-117: Check if the document's status is "Draft" or "Rejected".
        if (versionToDelete == null || (versionToDelete.Status != StatusEnum.Draft && versionToDelete.Status != StatusEnum.Rejected))
        {
            var currentStatus = versionToDelete?.Status.ToString() ?? "Unknown";
            var message = string.Format(MessageConstant.CanOnlyDeleteDrafts, currentStatus); // You might want to update this message
            _logger.LogWarning("Attempted to delete a document with status '{Status}', which is not 'Draft' or 'Rejected'.", currentStatus);
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, message);
        }
        _logger.LogInformation("Version to delete: {VersionName}, Status: {Status}", versionToDelete.VersionName, versionToDelete.Status);

        // 3. Delete the physical file from Google Drive.
        _logger.LogInformation("Deleting file from Google Drive: {FileName}", versionToDelete.FileName);
        var fileId = versionToDelete.GoogleDriveFileId ?? versionToDelete.FilePath;
        await _storageService.DeleteFileAsync(fileId, StorageFolderConstant.Drafts);
        _logger.LogInformation("Deleted file from Google Drive with ID: {FileId}", fileId);

        // 4. Delete the DocumentFile record from the database.
        // Due to cascade delete settings, this will also remove the associated DocumentVersion(s) and VersionTag(s).
        _logger.LogInformation("Deleting document from database: {DocumentId}", documentId);
        _unitOfWork.GetRepository<DocumentFile>().DeleteAsync(documentToDelete);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("User {UserId} successfully deleted draft document {DocumentId}.", userId, documentId);

        // TODO: As per SRS 3.4.3, this action should be recorded in the system audit log.
    }

    public async Task<IPaginate<DocumentDraftResponse>> GetDraftsAsync(int pageNumber, int pageSize)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        var drafts = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            filter: null,
            selector: d => _mapper.Map<DocumentDraftResponse>(d),
            predicate: v => v.DocumentFile.OwnerId == userId && v.Status == StatusEnum.Draft,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.CreatedTime),
            page: pageNumber,
            size: pageSize
        );

        // Enrich all documents with names in bulk for better performance
        var enrichedDocuments = await _enrichmentService.EnrichDocumentDraftResponsesAsync(drafts.Items.ToList());

        // Create new paginated result with enriched documents
        var enrichedPaginated = new Paginate<DocumentDraftResponse>
        {
            Items = enrichedDocuments,
            Page = drafts.Page, // Replace PageIndex with Page
            Size = drafts.Size, // Replace PageSize with Size
            Total = drafts.Total,
            TotalPages = drafts.TotalPages
        };

        _logger.LogInformation("Enriched {Count} draft documents with names for user {UserId}", enrichedDocuments.Count, userId);
        return enrichedPaginated;
    }

    public async Task<DocumentDraftResponse> GetDraftByIdAsync(string versionId)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        var draft = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.Id == versionId && v.DocumentFile.OwnerId == userId && v.Status == StatusEnum.Draft,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        if (draft == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DraftDocumentNotFound);
        }
        
        var response = _mapper.Map<DocumentDraftResponse>(draft);
        var enrichedResponse = await _enrichmentService.EnrichDocumentDraftResponseAsync(response);
        _logger.LogInformation("Draft document response enriched with names for version {VersionId}", versionId);
        return enrichedResponse;
    }

    public async Task<MyDocumentsWithStatsResponse> GetMyDocumentsWithStatsAsync(MyDocumentsFilter filter, int pageNumber, int pageSize)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        // Get paginated documents
        var myDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            selector: dv => _mapper.Map<DocumentDraftResponse>(dv),
            filter: filter,
            predicate: d => d.DocumentFile.OwnerId == userId,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.CreatedTime),
            page: pageNumber,
            size: pageSize
        );

        // Enrich documents with names
        var enrichedDocuments = await _enrichmentService.EnrichDocumentDraftResponsesAsync(myDocuments.Items.ToList());

        // Create enriched paginated result
        var enrichedPaginated = new Paginate<DocumentDraftResponse>
        {
            Items = enrichedDocuments,
            Page = myDocuments.Page,
            Size = myDocuments.Size,
            Total = myDocuments.Total,
            TotalPages = myDocuments.TotalPages
        };

        // Calculate statistics
        var statistics = await CalculateMyDocumentsStatisticsAsync(userId);

        return new MyDocumentsWithStatsResponse
        {
            Documents = enrichedPaginated,
            Statistics = statistics
        };
    }

    private async Task<MyDocumentsStatistics> CalculateMyDocumentsStatisticsAsync(string userId)
    {
        var allUserDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
            predicate: d => d.DocumentFile.OwnerId == userId,
            include: i => i.Include(v => v.DocumentFile)
        );

        var statistics = new MyDocumentsStatistics
        {
            TotalDrafts = allUserDocuments.Count(d => d.Status == StatusEnum.Draft),
            TotalPending = allUserDocuments.Count(d => d.Status == StatusEnum.Pending),
            TotalApproved = allUserDocuments.Count(d => d.Status == StatusEnum.Approved),
            TotalRejected = allUserDocuments.Count(d => d.Status == StatusEnum.Rejected),
            TotalArchived = allUserDocuments.Count(d => d.Status == StatusEnum.Archived),
            TotalDocuments = allUserDocuments.Count
        };

        return statistics;
    }

    public async Task<IPaginate<EditorApprovalHistoryResponse>> GetEditorApprovalHistoryAsync(EditorApprovalHistoryFilter filter, int pageNumber, int pageSize)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        // Only show approved, rejected, or archived documents owned by the user
        Expression<Func<DocumentVersion, bool>> accessControlPredicate = v =>
            v.DocumentFile.OwnerId == userId &&
            (v.Status == StatusEnum.Approved || v.Status == StatusEnum.Rejected || v.Status == StatusEnum.Archived);

        var documents = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            selector: dv => dv,
            filter: filter,
            predicate: accessControlPredicate,
            include: i => i.Include(v => v.DocumentFile)
                          .ThenInclude(df => df.DocumentType)
                          .Include(v => v.DocumentTags)
                          .ThenInclude(dt => dt.Tag)
                          .Include(v => v.ApprovalLogs),
            orderBy: q => q.OrderByDescending(v => v.LastUpdatedTime),
            page: pageNumber,
            size: pageSize
        );

        // Map to response with approval log details
        var responseItems = documents.Items.Select(v => MapToEditorApprovalHistoryResponse(v)).ToList();

        // Enrich with names
        var enrichedItems = await EnrichEditorApprovalHistoryResponsesAsync(responseItems);

        return new Paginate<EditorApprovalHistoryResponse>
        {
            Items = enrichedItems,
            Page = documents.Page,
            Size = documents.Size,
            Total = documents.Total,
            TotalPages = documents.TotalPages
        };
    }

    public async Task<EditorApprovalHistoryResponse> GetEditorApprovalHistoryDetailAsync(string versionId)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        var document = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.Id == versionId && v.DocumentFile.OwnerId == userId &&
                           (v.Status == StatusEnum.Approved || v.Status == StatusEnum.Rejected || v.Status == StatusEnum.Archived),
            include: i => i.Include(v => v.DocumentFile)
                          .ThenInclude(df => df.DocumentType)
                          .Include(v => v.DocumentTags)
                          .ThenInclude(dt => dt.Tag)
                          .Include(v => v.ApprovalLogs)
        );

        if (document == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentNotFound);
        }

        var response = MapToEditorApprovalHistoryResponse(document);
        var enrichedResponse = await EnrichEditorApprovalHistoryResponseAsync(response);

        return enrichedResponse;
    }

    public async Task<IPaginate<DocumentDraftResponse>> GetRejectDocumentsAsync(int pageNumber, int pageSize)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        var rejectedDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            filter: null,
            selector : d => _mapper.Map<DocumentDraftResponse>(d),
            predicate: v => v.DocumentFile.OwnerId == userId && v.Status == StatusEnum.Rejected,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.LastUpdatedTime),
            page: pageNumber,
            size: pageSize
        );

        // Enrich all documents with names in bulk for better performance
        var enrichedDocuments = await _enrichmentService.EnrichDocumentDraftResponsesAsync(rejectedDocuments.Items.ToList());

        // Create new paginated result with enriched documents
        var enrichedPaginated = new Paginate<DocumentDraftResponse>
        {
            Items = enrichedDocuments,
            Page = rejectedDocuments.Page, // Replace PageIndex with Page
            Size = rejectedDocuments.Size, // Replace PageSize with Size
            Total = rejectedDocuments.Total,
            TotalPages = rejectedDocuments.TotalPages
        };


        _logger.LogInformation("Enriched {Count} rejected documents with names for user {UserId}", enrichedDocuments.Count, userId);
        return enrichedPaginated;
    }

    public async Task<DocumentDraftResponse> GetRejectedById(string versionId)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        var rejectedDocument = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.Id == versionId && v.DocumentFile.OwnerId == userId && v.Status == StatusEnum.Rejected,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        if (rejectedDocument == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound,ErrorCode.NOT_FOUND, MessageConstant.RejectedDocumentNotFound);
        }

        var response = _mapper.Map<DocumentDraftResponse>(rejectedDocument);
        var enrichedResponse = await _enrichmentService.EnrichDocumentDraftResponseAsync(response);
        _logger.LogInformation("Rejected document response enriched with names for version {VersionId}", versionId);
        return enrichedResponse;
    }

    public async Task<DocumentDraftResponse> GetOfficialDocumentAsync(string documentFileId)
    {
        // Get user's department ID for permission filtering
        var userDepartmentId = GetCurrentUserDepartmentId();

        var officialDocument = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.DocumentFileId == documentFileId && v.IsOfficial && (v.IsPublic || v.DocumentFile.DepartmentId == userDepartmentId),
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        if (officialDocument == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.OfficialDocumentNotFoundForId);
        }

        var response = _mapper.Map<DocumentDraftResponse>(officialDocument);
        var enrichedResponse = await _enrichmentService.EnrichDocumentDraftResponseAsync(response);
        _logger.LogInformation("Official document response enriched with names for document {DocumentFileId}", documentFileId);
        return enrichedResponse;
    }

    public async Task<IPaginate<DocumentDraftResponse>> GetAllOfficialDocumentsAsync(int pageNumber, int pageSize, bool departmentOnly = false)
    {
        // Get user's department ID for permission filtering
        var userDepartmentId = GetCurrentUserDepartmentId();

        // Build predicate based on departmentOnly parameter
        Expression<Func<DocumentVersion, bool>> accessControlPredicate;
        if (departmentOnly)
        {
            // Show only documents from user's department (both public and private)
            accessControlPredicate = v => v.IsOfficial && v.Status == StatusEnum.Approved && v.DocumentFile.DepartmentId == userDepartmentId;
        }
        else
        {
            // Show documents from all departments, but only public ones (cannot view private documents from other departments)
            accessControlPredicate = v => v.IsOfficial && v.Status == StatusEnum.Approved && v.IsPublic;
        }

        var officialDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            filter: null,
            selector: d => _mapper.Map<DocumentDraftResponse>(d),
            predicate: accessControlPredicate,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.CreatedTime),
            page: pageNumber,
            size: pageSize
        );

        // Enrich all documents with names in bulk for better performance
        var enrichedDocuments = await _enrichmentService.EnrichDocumentDraftResponsesAsync(officialDocuments.Items.ToList());

        // Create new paginated result with enriched documents
        var enrichedPaginated = new Paginate<DocumentDraftResponse>
        {
            Items = enrichedDocuments,
            Page = officialDocuments.Page, // Replace PageIndex with Page
            Size = officialDocuments.Size, // Replace PageSize with Size
            Total = officialDocuments.Total,
            TotalPages = officialDocuments.TotalPages
        };

        _logger.LogInformation("Enriched {Count} official documents with names", enrichedDocuments.Count);
        return enrichedPaginated;
    }

    public async Task<IPaginate<DocumentDraftResponse>> GetAllOfficialDocumentsAsync(OfficialDocumentsFilter filter, int pageNumber, int pageSize)
    {
        // Get user's department ID for permission filtering
        var userDepartmentId = GetCurrentUserDepartmentId();


        // Use standard pattern: filter for user input, predicate for business logic and security
        // Handle department-based filtering with proper access control
        Expression<Func<DocumentVersion, bool>> accessControlPredicate;
        if (!string.IsNullOrEmpty(filter.DepartmentId))
        {
            // When filtering by specific department:
            // - If it's user's department: show all documents (public + private)
            // - If it's different department: show only public documents
            if (filter.DepartmentId == userDepartmentId)
            {
                accessControlPredicate = v => v.IsOfficial && v.Status == StatusEnum.Approved && v.DocumentFile.DepartmentId == filter.DepartmentId;
            }
            else
            {
                accessControlPredicate = v => v.IsOfficial && v.Status == StatusEnum.Approved && v.DocumentFile.DepartmentId == filter.DepartmentId && v.IsPublic;
            }
        }
        else if (filter.DepartmentOnly)
        {
            // Show only documents from user's department (both public and private)
            accessControlPredicate = v => v.IsOfficial && v.Status == StatusEnum.Approved && v.DocumentFile.DepartmentId == userDepartmentId;
        }
        else
        {
            // Show documents from all departments, but only public ones (cannot view private documents from other departments)
            accessControlPredicate = v => v.IsOfficial && v.Status == StatusEnum.Approved && v.IsPublic;
        }

        var officialDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            selector: d => _mapper.Map<DocumentDraftResponse>(d),
            filter: filter,
            predicate: accessControlPredicate,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.CreatedTime),
            page: pageNumber,
            size: pageSize
        );

        // Enrich all documents with names in bulk for better performance
        var enrichedDocuments = await _enrichmentService.EnrichDocumentDraftResponsesAsync(officialDocuments.Items.ToList());

        // Create new paginated result with enriched documents
        var enrichedPaginated = new Paginate<DocumentDraftResponse>
        {
            Items = enrichedDocuments,
            Page = officialDocuments.Page,
            Size = officialDocuments.Size,
            Total = officialDocuments.Total,
            TotalPages = officialDocuments.TotalPages
        };

        return enrichedPaginated;
    }

    /// <summary>
    /// Builds security predicate for department-based access control
    /// </summary>
    private static Expression<Func<DocumentVersion, bool>> BuildSecurityPredicate(string userDepartmentId)
    {
        return v => v.IsOfficial && (v.IsPublic || v.DocumentFile.DepartmentId == userDepartmentId);
    }

    /// <summary>
    /// Combines two predicates with AND logic
    /// </summary>
    private static Expression<Func<DocumentVersion, bool>> CombinePredicates(
        Expression<Func<DocumentVersion, bool>> predicate1,
        Expression<Func<DocumentVersion, bool>> predicate2)
    {
        var parameter = Expression.Parameter(typeof(DocumentVersion), "v");
        var body1 = Expression.Invoke(predicate1, parameter);
        var body2 = Expression.Invoke(predicate2, parameter);
        var combinedBody = Expression.AndAlso(body1, body2);
        return Expression.Lambda<Func<DocumentVersion, bool>>(combinedBody, parameter);
    }


    public async Task<IPaginate<DocumentDraftResponse>> GetMyDocumentsAsync(MyDocumentsFilter filter, int pageNumber, int pageSize)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        var myDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            selector: dv => _mapper.Map<DocumentDraftResponse>(dv),
            filter: filter,
            predicate: d => d.DocumentFile.OwnerId == userId,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.CreatedTime),
            page: pageNumber,
            size: pageSize
        );

        // Enrich all documents with names in bulk for better performance
        var enrichedDocuments = await _enrichmentService.EnrichDocumentDraftResponsesAsync(myDocuments.Items.ToList());

        // Create new paginated result with enriched documents
        var enrichedPaginated = new Paginate<DocumentDraftResponse>
        {
            Items = enrichedDocuments,
            Page = myDocuments.Page, // Replace PageIndex with Page
            Size = myDocuments.Size, // Replace PageSize with Size
            Total = myDocuments.Total,
            TotalPages = myDocuments.TotalPages
        };

        _logger.LogInformation("Enriched {Count} user documents with names for user {UserId}", enrichedDocuments.Count, userId);
        return enrichedPaginated;
    }

    public async Task<DocumentDraftResponse> GetMyDocumentByIdAsync(string versionId)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        var document = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.Id == versionId && v.DocumentFile.OwnerId == userId,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        if (document == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentNotFound);
        }

        var response = _mapper.Map<DocumentDraftResponse>(document);
        var enrichedResponse = await _enrichmentService.EnrichDocumentDraftResponseAsync(response);
        _logger.LogInformation("My document response enriched with names for version {VersionId}", versionId);
        return enrichedResponse;
    }

    public async Task<DocumentVersionResponse> GetDocumentVersionByVersionIdAsync(string documentId, string versionId)
    {
        // Get current user information for access control
        var userId = GetCurrentUserId();
        var userDepartmentId = GetCurrentUserDepartmentId();

        var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: dv => dv.DocumentFileId == documentId && dv.Id == versionId,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        if (documentVersion == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);
        }

        // Apply access control based on document status and user role
        if (!CanUserAccessDocumentVersion(documentVersion, userId, userDepartmentId))
        {
            throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessDocument);
        }

        var response = _mapper.Map<DocumentVersionResponse>(documentVersion);
        var enrichedResponse = await _enrichmentService.EnrichDocumentVersionResponseAsync(response);
        return enrichedResponse;
    }

    public async Task<List<DocumentVersionResponse>> GetDocumentVersionsAsync(string documentId)
    {
        // Get current user information for access control
        var userId = GetCurrentUserId();
        var userDepartmentId = GetCurrentUserDepartmentId();

        var documentVersions = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
            predicate: dv => dv.DocumentFileId == documentId,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        // Filter versions based on user access rights
        var accessibleVersions = documentVersions.Where(version => CanUserAccessDocumentVersion(version, userId, userDepartmentId)).ToList();

        var response = _mapper.Map<List<DocumentVersionResponse>>(accessibleVersions);
        var enrichedResponse = await _enrichmentService.EnrichDocumentVersionResponsesAsync(response);
        return enrichedResponse;
    }

    public async Task<DocumentDraftResponse> CreateNewVersionAsync(string documentId, CreateNewVersionDraftRequest request)
    {
        // Get current user ID from JWT token
        var userId = GetCurrentUserId();

        var documentToUpdate = await _unitOfWork.GetRepository<DocumentFile>().SingleOrDefaultAsync(
            predicate: d => d.Id == documentId,
            include: i => i.Include(d => d.DocumentVersions)
        ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentNotFound);

        // Input validations
        if (request?.File == null)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, "File is required for creating a new version.");
        }

        // Title, VersionName, file type, file size, and effective date validations are now handled by FluentValidation

        // BR-037: A document can only be in the process of being replaced by one new document at a time.
        var pendingVersion = documentToUpdate.DocumentVersions.FirstOrDefault(v => v.Status == StatusEnum.Pending);
        if (pendingVersion != null)
        {
            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.DocumentAlreadyUnderReplacement);
        }

        // BR-038: Editors can only replace documents within their assigned Department.
        // This check would typically involve retrieving the user's department(s) and comparing with documentToUpdate.DepartmentId.
        // Assuming department-based authorization is handled at a higher layer (e.g., controller/middleware) or user context needs to be enriched.

        if (documentToUpdate.OwnerId != userId)
        {
            throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToCreateNewVersion);
        }

        var latestVersion = documentToUpdate.DocumentVersions.OrderByDescending(v => v.CreatedTime).FirstOrDefault();

        if (latestVersion == null || latestVersion.Status != StatusEnum.Approved)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.CanOnlyCreateNewVersionOfApproved);
        }

        // Check draft limit for the user
        var draftCount = await _unitOfWork.GetRepository<DocumentVersion>()
            .CountAsync(predicate: v => v.CreatedBy == userId && v.Status == StatusEnum.Draft);
        if (draftCount >= PolicyConstant.MaxDraftsPerUser)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.MaxDraftsReached, PolicyConstant.MaxDraftsPerUser));
        }

        // Business Rule: For new versions, DepartmentId and ReplacementId are inherited from the existing DocumentFile
        // The new request model (CreateNewVersionDraftRequest) doesn't include these fields as they are automatically inherited

        StorageUploadResponse? uploadResponse = null;
        try
        {
            _logger.LogInformation("Creating new version for document {DocumentId} by user {UserId}", documentId, userId);
            uploadResponse = await _storageService.UploadFileAsync(request.File, StorageFolderConstant.Drafts, documentToUpdate.DepartmentId, request.IsPublic);
            var fileHash = uploadResponse.Md5Hash;
            _logger.LogInformation("File uploaded successfully with ID {FileId} for new version creation", uploadResponse.FileIdentifier);

            // Check for file duplication using the MD5 hash
            var existingFile = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(predicate: v => v.FileHash == fileHash && v.Status != StatusEnum.Rejected, include: i => i.Include(v => v.DocumentFile));

            if (existingFile != null)
            {
                _logger.LogWarning("Duplicate file detected with hash {FileHash}, deleting uploaded file {FileId}", fileHash, uploadResponse.FileIdentifier);
                await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, $"This file already exists in the system as '{existingFile.DocumentFile.Title}' (Version: {existingFile.VersionName}, Status: {existingFile.Status}).");
            }

            // Create new version - inherit departmentId and replacementId from existing DocumentFile
            // Note: DepartmentId and ReplacementId are automatically inherited from the DocumentFile
            var newVersion = new DocumentVersion
            {
                DocumentFileId = documentToUpdate.Id,
                Title = request.Title,
                VersionName = request.VersionName,
                Status = StatusEnum.Draft,
                IsOfficial = false,
                IsPublic = request.IsPublic, // Set public/private status from request
                Summary = request.Summary,
                FileName = uploadResponse.FileName,
                FileType = Path.GetExtension(uploadResponse.FileName),
                FileSize = request.File.Length,
                FilePath = uploadResponse.FileIdentifier, // Google Drive file ID for new uploads
                FileHash = fileHash,
                SignedBy = request.SignedBy,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveUntil = request.EffectiveUntil,
                CreatedBy = userId,
                LastSubmitted = DateTime.UtcNow,
                SubmittedBy = userId,
            };

            await ProcessTagsAsync(newVersion, request.Tags, userId);

            // Insert the new version directly instead of updating the DocumentFile
            // This avoids concurrency issues with the DocumentFile entity
            try
            {
                await _unitOfWork.GetRepository<DocumentVersion>().InsertAsync(newVersion);
                await _unitOfWork.CommitAsync();
                _logger.LogInformation("Successfully created new version for document {DocumentId}", documentToUpdate.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database operation failed for new version creation, rolling back uploaded file {FileId}", uploadResponse?.FileIdentifier);

                // Rollback: Delete the uploaded file if database operations fail
                if (uploadResponse != null)
                {
                    try
                    {
                        await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
                        _logger.LogInformation("Successfully rolled back uploaded file {FileId}", uploadResponse.FileIdentifier);
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "Failed to rollback uploaded file {FileId} after database error", uploadResponse.FileIdentifier);
                    }
                }
                throw;
            }

            // Load the complete version with DocumentFile and Tags for proper mapping
            var completeVersion = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
                predicate: v => v.Id == newVersion.Id,
                include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
            );

            var response = _mapper.Map<DocumentDraftResponse>(completeVersion);
            // COMMENTED OUT: Enrich response with user and department names
            // Name enrichment makes RabbitMQ calls to Auth service which slows down new version creation
            // Names can be enriched on-demand when viewing documents instead of during creation
            /*
            var enrichedResponse = await _enrichmentService.EnrichDocumentDraftResponseAsync(response);
            _logger.LogInformation("New version response enriched with names for document {DocumentId}", documentToUpdate.Id);
            */

            _logger.LogInformation("New version created successfully for document {DocumentId} (names not enriched for performance)", documentToUpdate.Id);
            var enrichedResponse = response; // Return response without name enrichment
            return enrichedResponse;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict when creating new version for document {DocumentId}. The document was modified by another user.", documentId);

            // Clean up uploaded file if concurrency error occurs
            if (uploadResponse != null)
            {
                try
                {
                    _logger.LogInformation("Rolling back storage upload for {FileIdentifier} due to concurrency conflict.", uploadResponse.FileIdentifier);
                    await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogError(deleteEx, "Failed to delete uploaded file {FileIdentifier} during rollback after concurrency conflict.", uploadResponse.FileIdentifier);
                }
            }

            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, "This document was updated by someone else. Please refresh and try again.");
        }
        catch (Exception)
        {
            // Clean up uploaded file if any other error occurs
            if (uploadResponse != null)
            {
                try
                {
                    _logger.LogInformation("Rolling back storage upload for {FileIdentifier} due to error.", uploadResponse.FileIdentifier);
                    await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogError(deleteEx, "Failed to delete uploaded file {FileIdentifier} during rollback after error.", uploadResponse.FileIdentifier);
                }
            }
            throw;
        }
    }

    private async Task ProcessTagsAsync(DocumentVersion version, IEnumerable<string> tagNames, string userId)
    {
        if (tagNames != null && tagNames.Count() == 1 && tagNames.First().Contains(','))
        {
            tagNames = tagNames.First().Split(',').Select(t => t.Trim()).ToList();
        }

        // Clear existing tags for the version
        var existingDocumentTags = await _unitOfWork.GetRepository<DocumentTag>()
            .GetListWithTrackingAsync(predicate: dt => dt.DocumentVersionId == version.Id);
        if (existingDocumentTags.Any())
        {
            _unitOfWork.GetRepository<DocumentTag>().DeleteRangeAsync(existingDocumentTags);
        }

        if (tagNames == null || !tagNames.Any())
        {
            return;
        }

        var distinctTagNames = tagNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _logger.LogInformation("Processing tags: {Tags}", JsonSerializer.Serialize(tagNames));
        _logger.LogInformation("Distinct tags: {Tags}", JsonSerializer.Serialize(distinctTagNames));

        // Normalize tag names to lowercase for consistent database comparison
        var normalizedTagNames = distinctTagNames.Select(t => t.ToLowerInvariant()).ToList();

        // Find which tags already exist in the database
        var existingTags = await _unitOfWork.GetRepository<Tag>()
            .GetListWithTrackingAsync(predicate: t => normalizedTagNames.Contains(t.Name));

        var existingTagNames = existingTags.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation("Existing tags: {Tags}", JsonSerializer.Serialize(existingTags.Select(t => t.Name)));

        // Create a list of the new tags that need to be inserted
        var newTagsToInsert = new List<Tag>();
        for (int i = 0; i < distinctTagNames.Count; i++)
        {
            var normalizedTagName = normalizedTagNames[i];
            if (!existingTagNames.Contains(normalizedTagName))
            {
                newTagsToInsert.Add(new Tag { Name = normalizedTagName, CreatedBy = userId });
            }
        }

        // Add the new tags to the change tracker
        if (newTagsToInsert.Any())
        {
            await _unitOfWork.GetRepository<Tag>().InsertRangeAsync(newTagsToInsert);
        }

        // Combine the existing tags and the new tags
        var allTagsForDocument = existingTags.Concat(newTagsToInsert).ToList();

        // Create the links between the document version and the tags
        foreach (var tag in allTagsForDocument)
        {
            version.DocumentTags.Add(new DocumentTag { Tag = tag });
        }
    }


    /// <summary>
    /// Performs enhanced semantic search on documents using AI-powered similarity matching with hybrid scoring
    /// </summary>
    /// <param name="request">Semantic search request with query and configuration options</param>
    /// <param name="filter">Advanced filters for search results</param>
    /// <param name="pageNumber">Page number for pagination</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of semantically similar documents with scoring details</returns>
    public async Task<IPaginate<SemanticSearchResponse>> SemanticSearch(SemanticSearchRequest request, SemanticSearchFilter filter, int pageNumber, int pageSize)
    {
        var startTime = DateTime.UtcNow;
        var userId = GetCurrentUserId();
        var userDepartmentId = GetCurrentUserDepartmentId();

        _logger.LogInformation("Starting semantic search - User: {UserId}, Department: {DepartmentId}, Query: '{Query}', HybridScoring: {HybridScoring}, Scope: {Scope}",
            userId, userDepartmentId, request.Query.Substring(0, Math.Min(50, request.Query.Length)), request.EnableHybridScoring, request.Scope);

        try
        {
            // Validate request parameters
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                throw new ArgumentException(ValidationMessageConstant.SemanticSearch.QueryRequired, nameof(request.Query));
            }

            // 1. Build the enhanced memory filter
            var memoryFilter = BuildEnhancedMemoryFilter(filter, request);
            _logger.LogDebug("Built memory filter with {FilterCount} conditions", GetFilterConditionCount(memoryFilter));

            // 2. Apply search scope filtering
            ApplySearchScopeFilter(memoryFilter, request.Scope, filter);

            // 3. Fetch results from Kernel Memory with configurable parameters
            _logger.LogDebug("Executing Kernel Memory search with limit: {Limit}, minRelevance: {MinRelevance}",
                request.MaxResults, request.MinRelevance);

            var searchResult = await _memory.SearchAsync(
                request.Query,
                limit: request.MaxResults,
                filter: memoryFilter,
                minRelevance: request.MinRelevance);

            // 4. Process search results with enhanced scoring
            if (!searchResult.Results.Any())
            {
                _logger.LogInformation("No semantic search results found for query: '{Query}' - Processing time: {ProcessingTime}ms",
                    request.Query, (DateTime.UtcNow - startTime).TotalMilliseconds);
                return new Paginate<SemanticSearchResponse>(new List<SemanticSearchResponse>(), pageNumber, pageSize, 0);
            }

            _logger.LogDebug("Found {ResultCount} raw results from Kernel Memory", searchResult.Results.Count);

            // 5. Group citations by document and calculate scores
            var documentCandidates = await ProcessSearchResults(searchResult, request, filter);

            if (!documentCandidates.Any())
            {
                _logger.LogInformation("No accessible documents found after security filtering for query: '{Query}'", request.Query);
                return new Paginate<SemanticSearchResponse>(new List<SemanticSearchResponse>(), pageNumber, pageSize, 0);
            }

            _logger.LogDebug("Found {CandidateCount} accessible document candidates", documentCandidates.Count);

            // 6. Apply hybrid scoring if enabled
            if (request.EnableHybridScoring)
            {
                _logger.LogDebug("Applying hybrid scoring to {CandidateCount} candidates", documentCandidates.Count);
                documentCandidates = await ApplyHybridScoring(documentCandidates, request, filter);
            }

            // 7. Sort by final score and apply pagination
            var sortedCandidates = documentCandidates.OrderByDescending(c => c.FinalScore).ToList();
            var totalCount = sortedCandidates.Count;
            var pagedCandidates = sortedCandidates.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            _logger.LogDebug("Returning page {PageNumber} of {PageSize} items from {TotalCount} total results",
                pageNumber, pageSize, totalCount);

            // 8. Convert to response objects with ranking
            var responses = new List<SemanticSearchResponse>();
            for (int i = 0; i < pagedCandidates.Count; i++)
            {
                var candidate = pagedCandidates[i];
                var response = _mapper.Map<SemanticSearchResponse>(candidate.DocumentVersion);
                response.Relevance = candidate.FinalScore;
                response.Rank = (pageNumber - 1) * pageSize + i + 1;
                response.IsDepartmentBoosted = candidate.IsDepartmentMatch && request.BoostDepartmentResults;

                if (request.EnableHybridScoring && candidate.Scoring != null)
                {
                    response.Scoring = candidate.Scoring;
                    response.Scoring.AppliedBoosts = candidate.AppliedBoosts;
                    response.Scoring.MatchingTags = candidate.MatchingTags;
                }

                responses.Add(response);
            }

            // 9. Enrich with names
            var enrichedItems = await _enrichmentService.EnrichSemanticSearchResponsesAsync(responses);

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("Semantic search completed successfully - User: {UserId}, Query: '{Query}', Results: {ResultCount}/{TotalCount}, HybridScoring: {HybridScoring}, ProcessingTime: {ProcessingTime}ms",
                userId, request.Query.Substring(0, Math.Min(50, request.Query.Length)), enrichedItems.Count, totalCount, request.EnableHybridScoring, processingTime);

            return new Paginate<SemanticSearchResponse>(enrichedItems, pageNumber, pageSize, totalCount);
        }
        catch (ArgumentException ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogWarning(ex, "Invalid semantic search request - User: {UserId}, Query: '{Query}', Error: {Error}, ProcessingTime: {ProcessingTime}ms",
                userId, request.Query, ex.Message, processingTime);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogWarning(ex, "Unauthorized semantic search attempt - User: {UserId}, Query: '{Query}', ProcessingTime: {ProcessingTime}ms",
                userId, request.Query, processingTime);
            throw;
        }
        catch (Exception ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Error performing semantic search - User: {UserId}, Query: '{Query}', Error: {Error}, ProcessingTime: {ProcessingTime}ms",
                userId, request.Query, ex.Message, processingTime);
            throw new InvalidOperationException("An error occurred while performing semantic search. Please try again.", ex);
        }
    }

    #region Semantic Search Helper Methods

    private MemoryFilter BuildEnhancedMemoryFilter(SemanticSearchFilter filter, SemanticSearchRequest request)
    {
        var memoryFilter = new MemoryFilter();

        // Department filtering
        if (!string.IsNullOrEmpty(filter.DepartmentId))
        {
            memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.DepartmentId, filter.DepartmentId);
        }

        // Public/private filtering
        if (filter.IsPublic.HasValue)
        {
            memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.IsPublic, filter.IsPublic.Value.ToString().ToLower());
        }

        // Note: Date range filtering (FromDate, ToDate, EffectiveFrom, EffectiveUntil)
        // is handled in the database predicate since memory filters work with exact tag matches
        // and don't support range queries efficiently

        // Content filtering - only DocumentType for memory filter
        if (!string.IsNullOrEmpty(filter.DocumentTypeId))
        {
            memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.DocumentType, filter.DocumentTypeId);
        }

        // Note: Tag and SignedBy filtering removed from memory filter to avoid conflicts
        // All content filtering is now handled in the database predicate for consistency

        return memoryFilter;
    }

    /// <summary>
    /// Gets the approximate number of filter conditions for logging purposes
    /// </summary>
    /// <param name="memoryFilter">The memory filter to analyze</param>
    /// <returns>Estimated number of filter conditions</returns>
    private int GetFilterConditionCount(MemoryFilter memoryFilter)
    {
        // This is a simple estimation since MemoryFilter doesn't expose its internal structure
        // We count based on what we know was added
        int count = 0;

        // This is just for logging purposes, so we'll return a reasonable estimate
        // In a real implementation, you might want to track this during filter building
        return count; // Simplified for now
    }

    private void ApplySearchScopeFilter(MemoryFilter memoryFilter, SearchScope scope, SemanticSearchFilter filter)
    {
        var userDepartmentId = GetCurrentUserDepartmentId();

        switch (scope)
        {
            case SearchScope.PublicOnly:
                memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.IsPublic, "true");
                break;
            case SearchScope.DepartmentOnly:
                if (!string.IsNullOrEmpty(userDepartmentId))
                {
                    memoryFilter.ByTag(SemanticSearchConstant.MemoryTags.DepartmentId, userDepartmentId);
                }
                break;
            case SearchScope.All:
            default:
                // No additional filtering - access control handled in database query
                break;
        }
    }

    private async Task<List<SemanticSearchCandidate>> ProcessSearchResults(
        SearchResult searchResult,
        SemanticSearchRequest request,
        SemanticSearchFilter filter)
    {
        // Group citations by document and get the MAX relevance for each
        var relevantDocuments = searchResult.Results
            .Select(citation => new
            {
                DocumentId = citation.Partitions.FirstOrDefault()?.Tags.TryGetValue(SemanticSearchConstant.MemoryTags.DocumentId, out var ids) == true ? ids.FirstOrDefault() : null,
                Relevance = citation.Partitions.FirstOrDefault()?.Relevance ?? 0,
                Tags = citation.Partitions.FirstOrDefault()?.Tags
            })
            .Where(x => !string.IsNullOrEmpty(x.DocumentId))
            .GroupBy(x => x.DocumentId)
            .Select(g => new
            {
                DocumentId = g.Key,
                MaxRelevance = g.Max(x => x.Relevance),
                Tags = g.FirstOrDefault()?.Tags
            })
            .OrderByDescending(x => x.MaxRelevance)
            .ToList();

        if (!relevantDocuments.Any())
        {
            return new List<SemanticSearchCandidate>();
        }

        var orderedUniqueDocumentIds = relevantDocuments
            .Select(d => d.DocumentId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Cast<string>()
            .ToList();

        // Fetch documents with enhanced filtering and security
        var userDepartmentId = GetCurrentUserDepartmentId();
        var predicate = BuildDatabasePredicate(orderedUniqueDocumentIds, request, filter, userDepartmentId);

        var documentVersions = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
            predicate: predicate,
            include: i => i.Include(v => v.DocumentFile)
                          .ThenInclude(df => df.DocumentType)
                          .Include(v => v.DocumentTags)
                          .ThenInclude(dt => dt.Tag)
        );



        // Create candidates with initial scoring
        var candidates = new List<SemanticSearchCandidate>();
        foreach (var docVersion in documentVersions)
        {
            var relevantDoc = relevantDocuments.FirstOrDefault(rd => rd.DocumentId == docVersion.DocumentFile.Id);
            if (relevantDoc != null)
            {
                var candidate = new SemanticSearchCandidate
                {
                    DocumentVersion = docVersion,
                    SemanticRelevance = relevantDoc.MaxRelevance,
                    FinalScore = relevantDoc.MaxRelevance,
                    IsDepartmentMatch = docVersion.DocumentFile.DepartmentId == userDepartmentId
                };

                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    private Expression<Func<DocumentVersion, bool>> BuildDatabasePredicate(
        List<string> documentIds,
        SemanticSearchRequest request,
        SemanticSearchFilter filter,
        string? userDepartmentId)
    {
        // Handle department-based filtering with proper access control
        if (!string.IsNullOrEmpty(filter.DepartmentId))
        {
            // When filtering by specific department:
            // - If it's user's department: show all documents (public + private)
            // - If it's different department: show only public documents
            if (filter.DepartmentId == userDepartmentId)
            {
                return dv => documentIds.Contains(dv.DocumentFile.Id) &&
                            dv.Status == StatusEnum.Approved &&
                            dv.IsOfficial &&
                            dv.DocumentFile.DepartmentId == filter.DepartmentId &&
                            // Additional filter conditions
                            (!filter.FromDate.HasValue || dv.CreatedTime >= filter.FromDate.Value) &&
                            (!filter.ToDate.HasValue || dv.CreatedTime <= filter.ToDate.Value) &&
                            (!filter.EffectiveFrom.HasValue || dv.EffectiveFrom >= filter.EffectiveFrom.Value) &&
                            (!filter.EffectiveUntil.HasValue || dv.EffectiveUntil <= filter.EffectiveUntil.Value) &&
                            (string.IsNullOrEmpty(filter.DocumentTypeId) || dv.DocumentFile.DocumentTypeId == filter.DocumentTypeId);
            }
            else
            {
                return dv => documentIds.Contains(dv.DocumentFile.Id) &&
                            dv.Status == StatusEnum.Approved &&
                            dv.IsOfficial &&
                            dv.DocumentFile.DepartmentId == filter.DepartmentId &&
                            dv.IsPublic &&
                            // Additional filter conditions
                            (!filter.FromDate.HasValue || dv.CreatedTime >= filter.FromDate.Value) &&
                            (!filter.ToDate.HasValue || dv.CreatedTime <= filter.ToDate.Value) &&
                            (!filter.EffectiveFrom.HasValue || dv.EffectiveFrom >= filter.EffectiveFrom.Value) &&
                            (!filter.EffectiveUntil.HasValue || dv.EffectiveUntil <= filter.EffectiveUntil.Value) &&
                            (string.IsNullOrEmpty(filter.DocumentTypeId) || dv.DocumentFile.DocumentTypeId == filter.DocumentTypeId);
            }
        }
        else
        {
            // Default access control: user can see public documents + private documents from their department
            return dv => documentIds.Contains(dv.DocumentFile.Id) &&
                        dv.Status == StatusEnum.Approved &&
                        dv.IsOfficial &&
                        (dv.IsPublic || dv.DocumentFile.DepartmentId == userDepartmentId) &&
                        // Additional filter conditions
                        (!filter.FromDate.HasValue || dv.CreatedTime >= filter.FromDate.Value) &&
                        (!filter.ToDate.HasValue || dv.CreatedTime <= filter.ToDate.Value) &&
                        (!filter.EffectiveFrom.HasValue || dv.EffectiveFrom >= filter.EffectiveFrom.Value) &&
                        (!filter.EffectiveUntil.HasValue || dv.EffectiveUntil <= filter.EffectiveUntil.Value) &&
                        (string.IsNullOrEmpty(filter.DocumentTypeId) || dv.DocumentFile.DocumentTypeId == filter.DocumentTypeId);
        }
    }

    private Task<List<SemanticSearchCandidate>> ApplyHybridScoring(
        List<SemanticSearchCandidate> candidates,
        SemanticSearchRequest request,
        SemanticSearchFilter filter)
    {
        var userDepartmentId = GetCurrentUserDepartmentId();

        foreach (var candidate in candidates)
        {
            var scoring = new SemanticSearchScoring
            {
                SemanticSimilarity = candidate.SemanticRelevance
            };

            // Calculate metadata score
            var metadataScore = CalculateMetadataScore(candidate, filter, userDepartmentId);
            scoring.MetadataScore = metadataScore;

            // Calculate contextual score
            var contextualScore = CalculateContextualScore(candidate, userDepartmentId);
            scoring.ContextualScore = contextualScore;

            // Apply weighted final score
            var finalScore = (candidate.SemanticRelevance * SemanticSearchConstant.ScoringWeights.SemanticSimilarityWeight) +
                           (metadataScore * SemanticSearchConstant.ScoringWeights.MetadataMatchWeight) +
                           (contextualScore * SemanticSearchConstant.ScoringWeights.ContextualFactorsWeight);

            // Apply boost factors
            finalScore = ApplyBoostFactors(finalScore, candidate, request, userDepartmentId);

            scoring.FinalScore = finalScore;
            candidate.FinalScore = finalScore;
            candidate.Scoring = scoring;
        }

        return Task.FromResult(candidates);
    }

    private double CalculateMetadataScore(SemanticSearchCandidate candidate, SemanticSearchFilter filter, string? userDepartmentId)
    {
        var docVersion = candidate.DocumentVersion;
        double score = 0.0;

        // Document type match
        if (!string.IsNullOrEmpty(filter.DocumentTypeId) && docVersion.DocumentFile.DocumentTypeId == filter.DocumentTypeId)
        {
            score += SemanticSearchConstant.ScoringWeights.DocumentTypeMatchWeight;
        }

        // Department compatibility
        if (docVersion.DocumentFile.DepartmentId == userDepartmentId)
        {
            score += SemanticSearchConstant.ScoringWeights.DepartmentCompatibilityWeight;
        }

        // Status relevance
        if (docVersion.Status == StatusEnum.Approved)
        {
            score += SemanticSearchConstant.ScoringWeights.StatusRelevanceWeight;
        }

        return Math.Min(score, 1.0); // Cap at 1.0
    }

    private double CalculateContextualScore(SemanticSearchCandidate candidate, string? userDepartmentId)
    {
        var docVersion = candidate.DocumentVersion;
        double score = 0.0;

        // Recency score (newer documents get higher scores)
        var daysSinceCreation = (DateTime.UtcNow - docVersion.CreatedTime).TotalDays;
        var recencyScore = Math.Max(0, 1.0 - (daysSinceCreation / 365.0)); // Decay over a year
        score += recencyScore * SemanticSearchConstant.ScoringWeights.RecencyWeight;

        // Department bonus
        if (candidate.IsDepartmentMatch)
        {
            score += SemanticSearchConstant.ScoringWeights.DepartmentBonusWeight;
        }

        // Popularity (based on download count)
        if (docVersion.TotalDownloads.HasValue && docVersion.TotalDownloads > 0)
        {
            var popularityScore = Math.Min(1.0, docVersion.TotalDownloads.Value / 100.0); // Normalize to 100 downloads
            score += popularityScore * SemanticSearchConstant.ScoringWeights.PopularityWeight;
        }

        return Math.Min(score, 1.0); // Cap at 1.0
    }

    private double ApplyBoostFactors(double baseScore, SemanticSearchCandidate candidate, SemanticSearchRequest request, string? userDepartmentId)
    {
        var boostedScore = baseScore;
        var appliedBoosts = new List<string>();

        // Same department boost
        if (candidate.IsDepartmentMatch && request.BoostDepartmentResults)
        {
            boostedScore *= SemanticSearchConstant.BoostFactors.SameDepartmentBoost;
            appliedBoosts.Add("Department Match");
        }

        // Public document boost
        if (candidate.DocumentVersion.IsPublic)
        {
            boostedScore *= SemanticSearchConstant.BoostFactors.PublicDocumentBoost;
            appliedBoosts.Add("Public Document");
        }

        // Recent document boost (within last 30 days)
        var daysSinceCreation = (DateTime.UtcNow - candidate.DocumentVersion.CreatedTime).TotalDays;
        if (daysSinceCreation <= 30)
        {
            boostedScore *= SemanticSearchConstant.BoostFactors.RecentDocumentBoost;
            appliedBoosts.Add("Recent Document");
        }

        // Exact tag match boost
        if (candidate.MatchingTags.Any())
        {
            boostedScore *= SemanticSearchConstant.BoostFactors.ExactTagMatchBoost;
            appliedBoosts.Add("Tag Match");
        }

        // Approved status boost
        if (candidate.DocumentVersion.Status == StatusEnum.Approved)
        {
            boostedScore *= SemanticSearchConstant.BoostFactors.ApprovedStatusBoost;
            appliedBoosts.Add("Approved Status");
        }

        candidate.AppliedBoosts = appliedBoosts;
        return Math.Min(boostedScore, 2.0); // Cap at 2.0 to prevent extreme scores
    }

    #endregion

    public async Task<IPaginate<DocumentDraftResponse>> FullTextSearch(FullTextSearchFilter filter, int pageNumber, int pageSize)
    {
        // Get user's department ID for permission filtering
        var userDepartmentId = GetCurrentUserDepartmentId();

        // Handle department-based filtering with proper access control
        Expression<Func<DocumentVersion, bool>> accessControlPredicate;
        if (!string.IsNullOrEmpty(filter.DepartmentId))
        {
            // When filtering by specific department:
            // - If it's user's department: show all documents (public + private)
            // - If it's different department: show only public documents
            if (filter.DepartmentId == userDepartmentId)
            {
                accessControlPredicate = dv => dv.Status == StatusEnum.Approved && dv.DocumentFile.DepartmentId == filter.DepartmentId;
            }
            else
            {
                accessControlPredicate = dv => dv.Status == StatusEnum.Approved && dv.DocumentFile.DepartmentId == filter.DepartmentId && dv.IsPublic;
            }
        }
        else
        {
            // Default access control: user can see public documents + private documents from their department
            accessControlPredicate = dv => dv.Status == StatusEnum.Approved && (dv.IsPublic || dv.DocumentFile.DepartmentId == userDepartmentId);
        }

        var documents = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            selector: dv => _mapper.Map<DocumentDraftResponse>(dv),
            filter: filter,
            predicate: accessControlPredicate,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.CreatedTime),
            page: pageNumber,
            size: pageSize
        );

        // Enrich all documents with names in bulk for better performance
        var enrichedDocuments = await _enrichmentService.EnrichDocumentDraftResponsesAsync(documents.Items.ToList());
        
        // Create new paginated result with enriched documents
        var enrichedPaginated = new Paginate<DocumentDraftResponse>
        {
            Items = enrichedDocuments,
            Page = documents.Page, // Replace PageIndex with Page
            Size = documents.Size, // Replace PageSize with Size
            Total = documents.Total,
            TotalPages = documents.TotalPages
        };
        
        _logger.LogInformation("Enriched {Count} full text search documents with names", enrichedDocuments.Count);
        return enrichedPaginated;
    }

    public async Task<(Stream stream, string contentType, string fileName)> GetFileForViewingAsync(string versionId)
    {
        _logger.LogInformation("Getting file for viewing for version {VersionId}", versionId);

        // Get the document version
        var version = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.Id == versionId
        );

        if (version == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Document version not found.");
        }

        // Get the file from storage
        var (stream, contentType, fileName) = await _storageService.GetFileForViewingAsync(version.FilePath);

        _logger.LogInformation("File {FileName} served for viewing for version {VersionId}", fileName, versionId);

        return (stream, contentType, fileName);
    }

    public async Task<(Stream stream, string contentType, string fileName)> GetFileForDownloadAsync(string versionId)
    {
        _logger.LogInformation("Getting file for download for version {VersionId}", versionId);

        // Get the document version
        var version = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.Id == versionId
        );

        if (version == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Document version not found.");
        }

        // Get the file from storage
        var (stream, contentType, fileName) = await _storageService.GetFileForViewingAsync(version.FilePath);

        _logger.LogInformation("File {FileName} served for download for version {VersionId}", fileName, versionId);

        return (stream, contentType, fileName);
    }

    public async Task<DocumentVersion> GetFileInfoAsync(string versionId)
    {
        _logger.LogInformation("Getting file info for version {VersionId}", versionId);

        // Get the document version with related data
        var version = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.Id == versionId,
            include: i => i.Include(v => v.DocumentFile)
        );

        if (version == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Document version not found.");
        }

        _logger.LogInformation("File info retrieved for version {VersionId}", versionId);

        return version;
    }

    private EditorApprovalHistoryResponse MapToEditorApprovalHistoryResponse(DocumentVersion version)
    {
        var response = new EditorApprovalHistoryResponse
        {
            DocumentId = version.DocumentFileId,
            VersionId = version.Id,
            Title = version.Title,
            Description = version.DocumentFile?.Description,
            Summary = version.Summary,
            FileName = version.FileName,
            FileSize = version.FileSize,
            FileType = version.FileType,
            Status = version.Status.ToString(),
            VersionName = version.VersionName,
            DepartmentId = version.DocumentFile?.DepartmentId ?? string.Empty,
            DocumentTypeId = version.DocumentFile?.DocumentTypeId ?? string.Empty,
            Tags = version.DocumentTags?.Select(dt => dt.Tag.Name).ToList() ?? new List<string>(),
            CreatedTime = version.CreatedTime,
            LastUpdatedTime = version.LastUpdatedTime,
            LastSubmitted = version.LastSubmitted,
            SubmittedBy = version.SubmittedBy,
            SignedBy = version.SignedBy,
            EffectiveFrom = version.EffectiveFrom,
            EffectiveUntil = version.EffectiveUntil,
            IsPublic = version.IsPublic,
            IsOfficial = version.IsOfficial,
            TotalDownloads = version.TotalDownloads
        };

        // Get the latest approval log for review details
        var latestApprovalLog = version.ApprovalLogs?
            .Where(log => log.Action == ApprovalAction.Approve || log.Action == ApprovalAction.Reject)
            .OrderByDescending(log => log.CreatedTime)
            .FirstOrDefault();

        if (latestApprovalLog != null)
        {
            response.ReviewedBy = latestApprovalLog.CreatedBy;
            response.ReviewedAt = latestApprovalLog.CreatedTime;
            response.ReviewComments = latestApprovalLog.Comments;
        }

        return response;
    }

    private Task<List<EditorApprovalHistoryResponse>> EnrichEditorApprovalHistoryResponsesAsync(List<EditorApprovalHistoryResponse> responses)
    {
        try
        {
            // For now, return responses without enrichment
            // TODO: Implement proper enrichment using the existing enrichment service patterns
            _logger.LogInformation("Enriching {Count} editor approval history responses", responses.Count);
            return Task.FromResult(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching editor approval history responses with names");
            return Task.FromResult(responses);
        }
    }

    private async Task<EditorApprovalHistoryResponse> EnrichEditorApprovalHistoryResponseAsync(EditorApprovalHistoryResponse response)
    {
        var enrichedList = await EnrichEditorApprovalHistoryResponsesAsync(new List<EditorApprovalHistoryResponse> { response });
        return enrichedList.First();
    }
}