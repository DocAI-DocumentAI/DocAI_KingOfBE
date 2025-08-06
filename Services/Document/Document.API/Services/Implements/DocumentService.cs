using AutoMapper;
using Document.API.Constants;
using Document.API.Models;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
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
using System.Security.Claims;
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

    public DocumentService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<DocumentService> logger, IKernelMemory memory, IStorageService storageService, IConfiguration configuration, IDocumentEnrichmentService enrichmentService, IHttpContextAccessor httpContextAccessor, IDocumentReplacementService replacementService, IDocumentPermissionManager permissionManager)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _memory = memory;
        _storageService = storageService;
        _enrichmentService = enrichmentService;
        _httpContextAccessor = httpContextAccessor;
        _replacementService = replacementService;
        _permissionManager = permissionManager;

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
        var user = _httpContextAccessor?.HttpContext?.User;
        return user?.FindFirst("departmentId")?.Value;
    }

    /// <summary>
    /// Gets the current user's ID from JWT token
    /// </summary>
    /// <returns>User ID</returns>
    private string GetCurrentUserId()
    {
        var user = _httpContextAccessor?.HttpContext?.User;
        var userIdClaim = user?.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("User ID not found in token");
        return userIdClaim;
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
                    predicate: d => d.Id.ToString() == request.ReplacementDocumentId,
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
        var uploadResponse = await _storageService.UploadFileAsync(request.File, StorageFolderConstant.Drafts, departmentId, request.IsPublic);
        var fileHash = uploadResponse.Md5Hash;

        // 5. Check for file duplication using the MD5 hash.
        var existingFile = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(predicate: v => v.FileHash == fileHash, include: i => i.Include(v => v.DocumentFile));

        if (existingFile != null)
        {
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
        // 7. Save to database
        await _unitOfWork.GetRepository<DocumentFile>().InsertAsync(documentFile);
        await _unitOfWork.CommitAsync();

        if (documentToReplace != null)
        {
            documentToReplace.IsReplaced = true;
            documentToReplace.LastUpdatedBy = userId;
            documentToReplace.LastUpdatedTime = DateTime.UtcNow;
            _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentToReplace);
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Marked document {OriginalDocumentId} as replaced by new document {NewDocumentId}", documentToReplace.Id, documentFile.Id);
        }

        _logger.LogInformation("Successfully created draft document {DocumentId}", documentFile.Id);

        // Apply Google Drive permissions based on document status (Draft = owner only)
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

        // 10. Enrich response with user and department names
        var enrichedResponse = await _enrichmentService.EnrichDocumentDraftResponseAsync(response);

        _logger.LogInformation("Draft document response enriched with names for document {DocumentId}", documentFile.Id);

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

        StorageUploadResponse uploadResponse = null;
        string fileHash = null;

        // First, get the version to update to access department info
        var versionToUpdate = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(
                predicate: v => v.Id == versionId,
                include: p => p.Include(v => v.DocumentFile)) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);

        if (request.File != null)
        {
            // File type and size validations are now handled by FluentValidation

            // Upload the new file to storage BEFORE starting the database transaction.
            _logger.LogInformation("Uploading new file to storage before database transaction begins.");
            uploadResponse = await _storageService.UploadFileAsync(request.File, StorageFolderConstant.Drafts, versionToUpdate.DocumentFile.DepartmentId, request.IsPublic);
            fileHash = uploadResponse.Md5Hash;
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
            if (fileHash != null)
            {
                var existingFile = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(predicate: v => v.FileHash == fileHash && v.Id != versionId && v.Status != StatusEnum.Rejected);
                if (existingFile != null)
                {
                    // If a duplicate is found, delete the file that was just uploaded.
                    await _storageService.DeleteFileAsync(uploadResponse.FileIdentifier, StorageFolderConstant.Drafts);
                    throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                        string.Format(MessageConstant.FileAlreadyExists, existingFile.DocumentFile.Title, existingFile.VersionName, existingFile.Status));
                }
            }

            // --- All validations passed. Apply changes. ---

            string oldFileNameToDelete = null;
            if (uploadResponse != null)
            {
                // Keep track of the old file name to delete it AFTER the transaction succeeds.
                oldFileNameToDelete = versionToUpdate.FileName;

                // Update version properties for the new file.
                versionToUpdate.FilePath = $"{StorageFolderConstant.Drafts}/{uploadResponse.FileName}";
                versionToUpdate.FileName = uploadResponse.FileName;
                versionToUpdate.FileType = Path.GetExtension(request.File.FileName);
                versionToUpdate.FileSize = request.File.Length;
                versionToUpdate.FileHash = fileHash;
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
            versionToUpdate.Status = versionToUpdate.Status == StatusEnum.Rejected ? StatusEnum.Draft : versionToUpdate.Status;

            // No need to call UpdateAsync since entities are now tracked by EF
            // Entity Framework will automatically detect changes and update them

            // Save changes to the database. This is the critical point.
            await _unitOfWork.CommitAsync();

            // ====================================================================================
            // STEP 3: Post-transaction cleanup.
            // ====================================================================================

            // If the commit was successful, we can now safely delete the old file from storage.
            if (!string.IsNullOrEmpty(oldFileNameToDelete))
            {
                _logger.LogInformation("Database commit successful. Deleting old file {OldFileName} from storage.", oldFileNameToDelete);
                // This operation can fail, but it won't roll back our database change.
                // You might want to add more robust error handling/logging here for orphaned files.
                await _storageService.DeleteFileAsync(oldFileNameToDelete, StorageFolderConstant.Drafts);
            }

            _logger.LogInformation("Successfully updated document version {VersionId}", versionId);

            // Reload the document version with DocumentType included to ensure proper mapping
            var updatedVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                );

            // Enrich response with user and department names
            var response = _mapper.Map<DocumentDraftResponse>(updatedVersion);
            var enrichedResponse = await _enrichmentService.EnrichDocumentDraftResponseAsync(response);

            _logger.LogInformation("Updated document response enriched with names for version {VersionId}", versionId);

            // Apply Google Drive permissions for updated draft (still owner only)
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
        _logger.LogInformation("Starting single-prompt AI analysis for file: {FileName}", file.FileName);

        // File type validation is now handled by FluentValidation at the controller level

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
            await using (var fs = new FileStream(tempFilePath, FileMode.Create)) { await file.CopyToAsync(fs); }

            // 1. Build the temporary Kernel Memory instance (same as before)
            await _memory.ImportDocumentAsync(tempFilePath, documentId: tempDocId);

            // 2. Engineer a single, comprehensive prompt asking for a JSON response
//            const string comprehensivePrompt = @"Analyze the document and return a single raw JSON object with the following keys:
//- ""summary"": HTML-formatted string. Start with bold document title and number. Summarize scope and key contents. Use <ul><li> for key points.
//Escape all quotes (\""). No markdown.
//- ""title"": string — official document title.
//- ""signedBy"": string — name of the signatory.
//- ""effectiveFrom"": 'yyyy-MM-dd' (or null).
//- ""effectiveUntil"": 'yyyy-MM-dd' (or null).
//- ""tags"": array of up to 5 relevant keywords.
//Requirements:
//- Respond **only** with a valid JSON object (no extra explanation or markdown).
//- If any value is missing, use null.
//- Escape all double-quotes inside HTML/markdown content (e.g., `\""`).
//";

            const string comprehensivePrompt = @"Analyze the document and return a single raw JSON object with the following keys:
- ""title"": string — official document title.
- ""versionName"": string — the document code (eg. 80_2025_QH15_649688)(or null).
- ""signedBy"": string — name of the signatory.
- ""effectiveFrom"": 'yyyy-MM-dd' (or null).
- ""effectiveUntil"": 'yyyy-MM-dd' (or null).
- ""tags"": array of up to 5 relevant keywords.
Requirements:
- Respond **only** with a valid JSON object (no extra explanation or markdown).
- If any value is missing, use null.
- Escape all double-quotes inside HTML/markdown content (e.g., `\""`).
";

            // 3. Make a single call to the AI model
            var filter = new MemoryFilter().ByDocument(tempDocId);

            
            MemoryAnswer answer = null;
            const int maxRetries = 3;
            const int delayBetweenRetriesMs = 1500;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                answer = await _memory.AskAsync(comprehensivePrompt, filter: filter);

                if (answer != null && answer.RelevantSources.Any() && !answer.Result.Contains("INFO NOT FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Successfully received valid AI response on attempt {AttemptNumber}.", attempt);
                    break;
                }

                _logger.LogWarning("AI analysis attempt {AttemptNumber} of {MaxRetries} failed or returned no relevant sources. Retrying...", attempt, maxRetries);

                if (attempt < maxRetries)
                {
                    await Task.Delay(delayBetweenRetriesMs);
                }
            }

            // 4. Parse the structured JSON response from the AI
            if (!answer.Result.Contains("INFO NOT FOUND", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Raw AI response for file {FileName}: {AiResponse}", file.FileName, answer.Result);
                ParseAiJsonResponse(answer.Result, response);
                _logger.LogInformation("Successfully parsed AI JSON response for file: {FileName}", file.FileName);

                // BR-077: Summaries should be under 1000 words.
                if (!string.IsNullOrEmpty(response.Summary))
                {
                    var words = response.Summary.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > PolicyConstant.MaxSummaryLength)
                    {
                        response.Summary = string.Join(" ", words.Take(PolicyConstant.MaxSummaryLength)) + "...";
                        _logger.LogWarning("AI-generated summary for file {FileName} exceeded {MaxLength} words and was truncated.", file.FileName, PolicyConstant.MaxSummaryLength);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during single-prompt AI analysis for file: {FileName}", file.FileName);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
            // Clean up the temporary document from Kernel Memory
            if(tempDocId != null)
            {
                await _memory.DeleteDocumentAsync(tempDocId);
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

            if (root.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.String)
                response.Summary = summary.GetString();

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
                    .ToList();
            }
        }
        catch (JsonException jex)
        {
            _logger.LogError(jex, "Failed to parse JSON response from AI. Possibly incomplete or malformed. Raw response: {AiResponse}", jsonResponse);
        }
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

        // BR-117: Check if the document's status is "Draft".
        if (versionToDelete == null || versionToDelete.Status != StatusEnum.Draft)
        {
            var currentStatus = versionToDelete?.Status.ToString() ?? "Unknown";
            var message = string.Format(MessageConstant.CanOnlyDeleteDrafts, currentStatus);
            _logger.LogWarning("Attempted to delete a document with status '{Status}', not 'Draft'.", currentStatus);
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
        var officialDocument = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.DocumentFileId == documentFileId && v.IsOfficial,
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

    public async Task<IPaginate<DocumentDraftResponse>> GetAllOfficialDocumentsAsync(int pageNumber, int pageSize)
    {
        // Get user's department ID for permission filtering
        var userDepartmentId = GetCurrentUserDepartmentId();

        var officialDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            filter: null,
            selector: d => _mapper.Map<DocumentDraftResponse>(d),
            predicate: v => v.IsOfficial && (v.IsPublic || v.DocumentFile.DepartmentId == userDepartmentId),
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
        var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: dv => dv.DocumentFileId == documentId && dv.Id == versionId,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        var response = _mapper.Map<DocumentVersionResponse>(documentVersion);
        var enrichedResponse = await _enrichmentService.EnrichDocumentVersionResponseAsync(response);
        return enrichedResponse;
    }

    public async Task<List<DocumentVersionResponse>> GetDocumentVersionsAsync(string documentId)
    {
        var documentVersions = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
            predicate: dv => dv.DocumentFileId == documentId,
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        var response = _mapper.Map<List<DocumentVersionResponse>>(documentVersions);
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

        StorageUploadResponse uploadResponse = null;
        try
        {
            uploadResponse = await _storageService.UploadFileAsync(request.File, StorageFolderConstant.Drafts, documentToUpdate.DepartmentId, request.IsPublic);
            var fileHash = uploadResponse.Md5Hash;

            // Check for file duplication using the MD5 hash
            var existingFile = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(predicate: v => v.FileHash == fileHash && v.Status != StatusEnum.Rejected, include: i => i.Include(v => v.DocumentFile));

            if (existingFile != null)
            {
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
            await _unitOfWork.GetRepository<DocumentVersion>().InsertAsync(newVersion);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Successfully created new version for document {DocumentId}", documentToUpdate.Id);

            // Load the complete version with DocumentFile and Tags for proper mapping
            var completeVersion = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
                predicate: v => v.Id == newVersion.Id,
                include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
            );

            var response = _mapper.Map<DocumentDraftResponse>(completeVersion);
            var enrichedResponse = await _enrichmentService.EnrichDocumentDraftResponseAsync(response);
            _logger.LogInformation("New version response enriched with names for document {DocumentId}", documentToUpdate.Id);
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
        
        // Find which tags already exist in the database
        var existingTags = await _unitOfWork.GetRepository<Tag>()
            .GetListWithTrackingAsync(predicate: t => distinctTagNames.Contains(t.Name));

        var existingTagNames = existingTags.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation("Existing tags: {Tags}", JsonSerializer.Serialize(existingTags.Select(t => t.Name)));

        // Create a list of the new tags that need to be inserted
        var newTagsToInsert = new List<Tag>();
        foreach (var tagName in distinctTagNames)
        {
            if (!existingTagNames.Contains(tagName))
            {
                newTagsToInsert.Add(new Tag { Name = tagName.ToLowerInvariant(), CreatedBy = userId });
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


    public async Task<IPaginate<SemanticSearchResponse>> SemanticSearch(SemanticSearchRequest request, SemanticSearchFilter filter, int pageNumber, int pageSize)
    {
        // 1. Build the filter
        var memoryFilter = new MemoryFilter();
        if (!string.IsNullOrEmpty(filter.DepartmentId))
        {
            memoryFilter.ByTag("departmentId", filter.DepartmentId);
        }
        if (filter.IsPublic.HasValue)
        {
            memoryFilter.ByTag("isPublic", filter.IsPublic.Value.ToString());
        }
        if (filter.EffectiveFrom.HasValue)
        {
            memoryFilter.ByTag("effectiveFrom", filter.EffectiveFrom.Value.ToString("yyyy-MM-dd"));
        }
        if (filter.EffectiveUntil.HasValue)
        {
            memoryFilter.ByTag("effectiveUntil", filter.EffectiveUntil.Value.ToString("yyyy-MM-dd"));
        }
        if (!string.IsNullOrEmpty(filter.SignedBy))
        {
            memoryFilter.ByTag("signedBy", filter.SignedBy);
        }
        if (filter.Tags != null && filter.Tags.Any())
        {
            foreach (var tag in filter.Tags)
            {
                memoryFilter.ByTag("tags", tag);
            }
        }

        // 2. Fetch results from Kernel Memory
        var searchResult = await _memory.SearchAsync(
            request.Query,
            limit: 100,
            filter: memoryFilter,
            minRelevance: 0.2);

        // 3. Group citations by document and get the MAX relevance for each
        var relevantDocuments = searchResult.Results
            .Select(citation => new
            {
                // Extract the documentId from the tags
                DocumentId = citation.Partitions.FirstOrDefault()?.Tags.TryGetValue("documentId", out var ids) == true ? ids.FirstOrDefault() : null,
                // Get the relevance score for this specific citation (chunk)
                Relevance = citation.Partitions.FirstOrDefault()?.Relevance ?? 0
            })
            .Where(x => !string.IsNullOrEmpty(x.DocumentId))
            .GroupBy(x => x.DocumentId) // Group all chunks by their parent document ID
            .Select(g => new
            {
                DocumentId = g.Key,
                // Find the highest relevance score among all chunks for that document
                MaxRelevance = g.Max(x => x.Relevance)
            })
            .OrderByDescending(x => x.MaxRelevance) // Order documents by their best score
            .ToList();

        if (!relevantDocuments.Any())
        {
            return new Paginate<SemanticSearchResponse>(new List<SemanticSearchResponse>(), pageNumber, pageSize, 0);
        }

        // Create a lookup map for relevance scores for easy access later
        var relevanceMap = relevantDocuments.ToDictionary(d => d.DocumentId, d => d.MaxRelevance);
        var orderedUniqueDocumentIds = relevantDocuments.Select(d => d.DocumentId).ToList();

        // 4. Fetch all documents in ONE query from the database with department-based filtering
        var userDepartmentId = GetCurrentUserDepartmentId();
        var documentVersions = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
            predicate: dv => orderedUniqueDocumentIds.Contains(dv.DocumentFile.Id) &&
                            dv.Status == StatusEnum.Approved &&
                            (dv.IsPublic || dv.DocumentFile.DepartmentId == userDepartmentId),
            include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        // 5. Map results and add the relevance score
        var documentResponses = new List<SemanticSearchResponse>();
        foreach (var docVersion in documentVersions)
        {
            var response = _mapper.Map<SemanticSearchResponse>(docVersion);
            // Assign the stored relevance score
            response.Relevance = relevanceMap.TryGetValue(docVersion.DocumentFile.Id, out var relevance) ? relevance : 0;
            documentResponses.Add(response);
        }

        // Order the final list by the original relevance ranking
        var orderedResponses = documentResponses.OrderByDescending(r => r.Relevance).ToList();

        // 6. Apply in-memory pagination
        var totalCount = orderedResponses.Count;
        var pagedItems = orderedResponses.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        // 7. Enrich the paginated items with names
        var enrichedItems = await _enrichmentService.EnrichSemanticSearchResponsesAsync(pagedItems);
        _logger.LogInformation("Enriched {Count} semantic search results with names", enrichedItems.Count);

        return new Paginate<SemanticSearchResponse>(enrichedItems, pageNumber, pageSize, totalCount);
    }

    public async Task<IPaginate<DocumentDraftResponse>> FullTextSearch(FullTextSearchFilter filter, int pageNumber, int pageSize)
    {
        // Get user's department ID for permission filtering
        var userDepartmentId = GetCurrentUserDepartmentId();

        var documents = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            selector: dv => _mapper.Map<DocumentDraftResponse>(dv),
            filter: filter,
            predicate: dv => dv.Status == StatusEnum.Approved && (dv.IsPublic || dv.DocumentFile.DepartmentId == userDepartmentId),
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
}