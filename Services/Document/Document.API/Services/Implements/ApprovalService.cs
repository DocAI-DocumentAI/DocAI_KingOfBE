using AutoMapper;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Enums;
using Document.Domain.Models;
using Document.Domain.Model;
using Document.Infrastructure.Paginate;
using Document.Infrastructure.Repository.Interfaces;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.Office2010.Word;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using Shared.Exceptions;
using System.Security.Claims;

namespace Document.API.Services.Implements
{
    public class ApprovalService : IApprovalService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<ApprovalService> _logger;
        private readonly IStorageService _storageService;
        private readonly IKernelMemory _memory;
        private readonly IDocumentEnrichmentService _enrichmentService;
        private readonly IDocumentPermissionManager _permissionManager;
        private readonly IDocumentNotificationService _notificationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApprovalService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<ApprovalService> logger, IStorageService storageService, IKernelMemory kernelMemory, IDocumentEnrichmentService enrichmentService, IDocumentPermissionManager permissionManager, IDocumentNotificationService notificationService, IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _storageService = storageService;
            _memory = kernelMemory;
            _enrichmentService = enrichmentService;
            _permissionManager = permissionManager;
            _notificationService = notificationService;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<IPaginate<PendingDocumentResponse>> GetApprovalQueueAsync(Document.Infrastructure.Filter.ApprovalQueueFilter filter, int pageNumber, int pageSize)
        {
            var departmentId = GetCurrentUserDepartmentId() ?? throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessApprovalQueue);
            var pendingDocuments = await _unitOfWork.GetRepository<DocumentVersion>()
                .GetPagingListAsync(
                selector: v => _mapper.Map<PendingDocumentResponse>(v),
                filter: filter,
                include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType),
                predicate: v => (v.Status == StatusEnum.Pending || v.Status == StatusEnum.Rejected) && v.DocumentFile.DepartmentId == departmentId,
                orderBy: v => v.OrderByDescending(v => v.LastSubmitted),
                page: pageNumber,
                size: pageSize
                );

            // Enrich all pending documents with names in bulk for better performance
            var enrichedPendingDocuments = await _enrichmentService.EnrichPendingDocumentResponsesAsync(pendingDocuments.Items.ToList());

            // Create new paginated result with enriched pending documents
            var enrichedPaginated = new Paginate<PendingDocumentResponse>
            {
                Items = enrichedPendingDocuments,
                Page = pendingDocuments.Page,
                Size = pendingDocuments.Size,
                Total = pendingDocuments.Total,
                TotalPages = pendingDocuments.TotalPages
            };

            _logger.LogInformation("Enriched {Count} pending documents with names for department {DepartmentId}", enrichedPendingDocuments.Count, departmentId);
            return enrichedPaginated;
        }

        public async Task ClaimDocumentForReviewAsync(string versionId)
        {
            // Get current user ID and department ID from JWT token
            var userId = GetCurrentUserId();
            var managerDepartmentId = GetCurrentUserDepartmentId();

            var versionToClaim = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);

            if (versionToClaim.Status != StatusEnum.Pending)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.NotPendingApproval, versionToClaim.Status));
            }

            // Check if manager's department matches the document's department
            if (versionToClaim.DocumentFile.DepartmentId != managerDepartmentId)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessApprovalQueue);
            }

            var existingClaim = await _unitOfWork.GetRepository<ApprovalClaim>()
                .SingleOrDefaultAsync(predicate: ac => ac.DocumentVersionId == versionId && ac.IsActive);

            if (existingClaim != null)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, string.Format(MessageConstant.DocumentAlreadyClaimed, existingClaim.ClaimedBy));
            }

            var newClaim = new ApprovalClaim
            {
                DocumentVersionId = versionId,
                ClaimedBy = userId,
                ClaimedAt = DateTime.UtcNow,
                IsActive = true,
                CreatedBy = userId
            };

            await _unitOfWork.GetRepository<ApprovalClaim>().InsertAsync(newClaim);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Document version {VersionId} claimed for review by user {UserId}", versionId, userId);
        }

        public async Task ReleaseClaimAsync(string versionId)
        {
            // Get current user ID from JWT token
            var userId = GetCurrentUserId();
            var existingClaim = await _unitOfWork.GetRepository<ApprovalClaim>()
                .SingleOrDefaultAsync(predicate: ac => ac.DocumentVersionId == versionId && ac.IsActive);

            if (existingClaim == null)
            {
                throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.ClaimNotFound);
            }

            if (existingClaim.ClaimedBy != userId)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToReleaseClaim);
            }

            existingClaim.IsActive = false;
            existingClaim.LastUpdatedBy = userId;
            existingClaim.LastUpdatedTime = DateTime.UtcNow;

            _unitOfWork.GetRepository<ApprovalClaim>().UpdateAsync(existingClaim);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Document version {VersionId} claim released by user {UserId}", versionId, userId);
        }

        public async Task<ApprovalQueueDetailResponse> GetApprovalQueueDetailAsync(string versionId)
        {
            // Get current manager's department ID from JWT token
            var managerDepartmentId = GetCurrentUserDepartmentId();

            var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId && (v.Status == StatusEnum.Pending || v.Status == StatusEnum.Rejected),
                    include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag).Include(v => v.ApprovalClaim)
                );

            if (documentVersion == null)
            {
                throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);
            }

            // Check if manager's department matches the document's department
            if (documentVersion.DocumentFile.DepartmentId != managerDepartmentId)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessApprovalQueue);
            }

            var response = _mapper.Map<ApprovalQueueDetailResponse>(documentVersion);
            var enrichedResponse = await _enrichmentService.EnrichApprovalQueueDetailResponseAsync(response);

            _logger.LogInformation("Enriched approval queue detail response with names for version {VersionId}", versionId);
            return enrichedResponse;
        }

        public async Task ReviewDocument(string versionId, ReviewDocumentRequest request)
        {
            // Get current user ID and department ID from JWT token
            var userId = GetCurrentUserId();
            var managerDepartmentId = GetCurrentUserDepartmentId();

            var versionToReview = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(
                predicate: v => v.Id == versionId,
                include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
            ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);
            var documentFile = versionToReview.DocumentFile;

            // Declare variables at method level for broader scope
            DocumentFile? replacedDocument = null;

            // --- Permission and State Validation ---
            if (documentFile.DepartmentId != managerDepartmentId)
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessApprovalQueue);

            if (versionToReview.Status != StatusEnum.Pending)
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.NotPendingApproval, versionToReview.Status));

            ApprovalAction logAction;

            if (request.IsApproved)
            {
                // ========================================
                // DOCUMENT APPROVAL PROCESS
                // ========================================
                // This section handles three scenarios:
                // 1. NEW DOCUMENT: First version of a document
                // 2. NEW VERSION: New version of existing document (archives previous version)
                // 3. REPLACEMENT: New document that replaces another document (archives replaced document)

                // Get previous approved version of the SAME document (for versioning)
                var previousApprovedVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: v => v.DocumentFileId == documentFile.Id && v.Status == StatusEnum.Approved,
                        include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                    );

                // Check if this document REPLACES another document (different from versioning)
                if (!string.IsNullOrEmpty(documentFile.ReplacementId))
                {
                    replacedDocument = await _unitOfWork.GetRepository<DocumentFile>()
                        .SingleOrDefaultAsync(
                            predicate: df => df.Id == documentFile.ReplacementId && !df.IsReplaced,
                            include: i => i.Include(df => df.DocumentVersions.Where(v => v.Status == StatusEnum.Approved))
                                          .ThenInclude(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                        );
                }

                try
                {
                    // ========================================
                    // SCENARIO 3: DOCUMENT REPLACEMENT HANDLING
                    // ========================================
                    // If this document replaces another document, archive the replaced document
                    if (replacedDocument != null)
                    {
                        var replacedApprovedVersion = replacedDocument.DocumentVersions.FirstOrDefault(v => v.Status == StatusEnum.Approved);
                        if (replacedApprovedVersion != null)
                        {
                            // Move replaced document to archived folder
                            var replacedFileId = replacedApprovedVersion.GoogleDriveFileId ?? replacedApprovedVersion.FilePath;
                            await _storageService.MoveFileAsync(replacedFileId, StorageFolderConstant.Approved, StorageFolderConstant.Archived,
                                replacedDocument.DepartmentId, replacedApprovedVersion.IsPublic);

                            // Archive replaced document in Kernel Memory
                            var replacedVersionKmId = replacedApprovedVersion.Id.ToString();
                            var replacedTags = new TagCollection
                            {
                                { SemanticSearchConstant.MemoryTags.Status, "archived" },
                                { SemanticSearchConstant.MemoryTags.DepartmentId, replacedDocument.DepartmentId },
                                { SemanticSearchConstant.MemoryTags.DocumentId, replacedDocument.Id.ToString() },
                                { SemanticSearchConstant.MemoryTags.Version, replacedApprovedVersion.VersionName },
                                { SemanticSearchConstant.MemoryTags.ApprovalDate, replacedApprovedVersion.CreatedTime.ToString("yyyy-MM-dd") },
                                { SemanticSearchConstant.MemoryTags.OwnerId, replacedDocument.OwnerId },
                                { SemanticSearchConstant.MemoryTags.IsPublic, replacedApprovedVersion.IsPublic.ToString() },
                                { SemanticSearchConstant.MemoryTags.EffectiveFrom, replacedApprovedVersion.EffectiveFrom?.ToString("yyyy-MM-dd") },
                                { SemanticSearchConstant.MemoryTags.EffectiveUntil, replacedApprovedVersion.EffectiveUntil?.ToString("yyyy-MM-dd") },
                                { SemanticSearchConstant.MemoryTags.SignedBy, replacedApprovedVersion.SignedBy },
                                { SemanticSearchConstant.MemoryTags.DocumentType, replacedDocument.DocumentTypeId },
                                { SemanticSearchConstant.MemoryTags.IsOfficial, "false" },
                                { "replacedBy", documentFile.Id.ToString() },
                                { "replacementReason", "Document replaced by newer version" }
                            };

                            if (replacedApprovedVersion.DocumentTags != null)
                            {
                                foreach (var docTag in replacedApprovedVersion.DocumentTags)
                                {
                                    replacedTags.Add(SemanticSearchConstant.MemoryTags.Tags, docTag.Tag.Name);
                                }
                            }

                            using (var fileStream = await _storageService.DownloadFileAsync(replacedApprovedVersion.FilePath))
                            {
                                await _memory.ImportDocumentAsync(fileStream, replacedApprovedVersion.FileName, documentId: replacedVersionKmId, tags: replacedTags);
                            }

                            // Update database - mark replaced document as archived
                            replacedApprovedVersion.Status = StatusEnum.Archived;
                            replacedApprovedVersion.IsOfficial = false;
                            replacedDocument.IsReplaced = true;
                            await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(replacedApprovedVersion);
                            await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(replacedDocument);

                            _logger.LogInformation("Archived replaced document {ReplacedDocumentId} and updated its AI tags.", replacedDocument.Id);
                        }
                    }

                    // ========================================
                    // SCENARIO 2: VERSION ARCHIVING HANDLING
                    // ========================================
                    // If there's a previous approved version of the SAME document, archive it
                    if (previousApprovedVersion != null)
                    {
                        // Use Google Drive file ID for move operation
                        var previousFileId = previousApprovedVersion.GoogleDriveFileId ?? previousApprovedVersion.FilePath;
                        await _storageService.MoveFileAsync(previousFileId, StorageFolderConstant.Approved, StorageFolderConstant.Archived,
                            previousApprovedVersion.DocumentFile.DepartmentId, previousApprovedVersion.IsPublic);
                        // FilePath remains the Google Drive file ID - no change needed
                    }

                    // ========================================
                    // CURRENT DOCUMENT APPROVAL
                    // ========================================
                    // Move the current document from Pending to Approved folder
                    var currentFileId = versionToReview.GoogleDriveFileId ?? versionToReview.FilePath;
                    await _storageService.MoveFileAsync(currentFileId, StorageFolderConstant.Pending, StorageFolderConstant.Approved,
                        versionToReview.DocumentFile.DepartmentId, versionToReview.IsPublic);
                    // FilePath remains the Google Drive file ID - no change needed

                    var fileExists = false;
                    var retryCount = 0;
                    while (!fileExists && retryCount < 5)
                    {
                        fileExists = await _storageService.FileExistsAsync(currentFileId);
                        if (!fileExists)
                        {
                            await Task.Delay(500);
                            retryCount++;
                        }
                    }

                    if (!fileExists)
                    {
                        throw new ErrorException(StatusCodes.Status500InternalServerError, ErrorCode.INTERNAL_SERVER_ERROR, MessageConstant.FileNotAvailableInApprovedFolder);
                    }

                    if (previousApprovedVersion != null)
                    {
                        var previousVersionKmId = previousApprovedVersion.Id.ToString();
                        var oldTags = new TagCollection
                        {
                            { SemanticSearchConstant.MemoryTags.Status, "archived" },
                            { SemanticSearchConstant.MemoryTags.DepartmentId, documentFile.DepartmentId },
                            { SemanticSearchConstant.MemoryTags.DocumentId, documentFile.Id.ToString() },
                            { SemanticSearchConstant.MemoryTags.Version, previousApprovedVersion.VersionName },
                            { SemanticSearchConstant.MemoryTags.ApprovalDate, previousApprovedVersion.CreatedTime.ToString("yyyy-MM-dd") },
                            { SemanticSearchConstant.MemoryTags.OwnerId, documentFile.OwnerId },
                            { SemanticSearchConstant.MemoryTags.IsPublic, previousApprovedVersion.IsPublic.ToString() },
                            { SemanticSearchConstant.MemoryTags.EffectiveFrom, previousApprovedVersion.EffectiveFrom?.ToString("yyyy-MM-dd") },
                            { SemanticSearchConstant.MemoryTags.EffectiveUntil, previousApprovedVersion.EffectiveUntil?.ToString("yyyy-MM-dd") },
                            { SemanticSearchConstant.MemoryTags.SignedBy, previousApprovedVersion.SignedBy },
                            { SemanticSearchConstant.MemoryTags.DocumentType, previousApprovedVersion.DocumentFile.DocumentTypeId },
                            { SemanticSearchConstant.MemoryTags.IsOfficial, previousApprovedVersion.IsOfficial.ToString().ToLower() }
                        };

                        if (previousApprovedVersion.DocumentTags != null)
                        {
                            foreach (var docTag in previousApprovedVersion.DocumentTags)
                            {
                                oldTags.Add(SemanticSearchConstant.MemoryTags.Tags, docTag.Tag.Name);
                            }
                        }
                        using (var fileStream = await _storageService.DownloadFileAsync(previousApprovedVersion.FilePath))
                        {
                            await _memory.ImportDocumentAsync(fileStream, previousApprovedVersion.FileName, documentId: previousVersionKmId, tags: oldTags);
                        }

                        previousApprovedVersion.Status = StatusEnum.Archived;
                        previousApprovedVersion.IsOfficial = false;
                        await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(previousApprovedVersion);
                        _logger.LogInformation("Archived previous version {VersionId} and updated its AI tags.", previousApprovedVersion.Id);

                        // Update permissions for the archived document (will be done after commit)
                        // Note: Permission update will happen after the main commit
                    }

                    // ========================================
                    // UPDATE DATABASE STATUS
                    // ========================================
                    // Mark the current document as approved and official
                    versionToReview.Status = StatusEnum.Approved;
                    versionToReview.IsOfficial = true;
                    logAction = ApprovalAction.Approve;

                    // ========================================
                    // KERNEL MEMORY INDEXING
                    // ========================================
                    // Index the approved document in Kernel Memory with complete metadata
                    var tags = new TagCollection
                    {
                        { SemanticSearchConstant.MemoryTags.Status, "approved" },
                        { SemanticSearchConstant.MemoryTags.DepartmentId, documentFile.DepartmentId },
                        { SemanticSearchConstant.MemoryTags.DocumentId, documentFile.Id.ToString() },
                        { SemanticSearchConstant.MemoryTags.Version, versionToReview.VersionName },
                        { SemanticSearchConstant.MemoryTags.ApprovalDate, DateTime.UtcNow.ToString("yyyy-MM-dd") },
                        { SemanticSearchConstant.MemoryTags.OwnerId, documentFile.OwnerId },
                        { SemanticSearchConstant.MemoryTags.IsPublic, versionToReview.IsPublic.ToString() },
                        { SemanticSearchConstant.MemoryTags.EffectiveFrom, versionToReview.EffectiveFrom?.ToString("yyyy-MM-dd") },
                        { SemanticSearchConstant.MemoryTags.EffectiveUntil, versionToReview.EffectiveUntil?.ToString("yyyy-MM-dd") },
                        { SemanticSearchConstant.MemoryTags.SignedBy, versionToReview.SignedBy },
                        { SemanticSearchConstant.MemoryTags.DocumentType, versionToReview.DocumentFile.DocumentTypeId },
                        { SemanticSearchConstant.MemoryTags.IsOfficial, versionToReview.IsOfficial.ToString().ToLower() }
                    };

                    if (versionToReview.DocumentTags != null)
                    {
                        foreach (var docTag in versionToReview.DocumentTags)
                        {
                            tags.Add(SemanticSearchConstant.MemoryTags.Tags, docTag.Tag.Name);
                        }
                    }

                    var versionKmId = versionToReview.Id.ToString();
                    using (var fileStream = await _storageService.DownloadFileAsync(versionToReview.FilePath))
                    {
                        _logger.LogInformation("Content length: {Length}, Name: {FileName}", fileStream.Length, versionToReview.FileName);
                        await _memory.ImportDocumentAsync(fileStream, versionToReview.FileName, documentId: versionKmId, tags: tags);
                    }
                    _logger.LogInformation("Indexed approved version {VersionId} in Kernel Memory with structured tags.", versionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the approval process for version {VersionId}. Reverting storage changes.", versionId);

                    // Rollback replaced document if it was moved
                    if (replacedDocument != null)
                    {
                        var replacedApprovedVersion = replacedDocument.DocumentVersions.FirstOrDefault(v => v.Status == StatusEnum.Approved);
                        if (replacedApprovedVersion != null)
                        {
                            var replacedFileId = replacedApprovedVersion.GoogleDriveFileId ?? replacedApprovedVersion.FilePath;
                            await _storageService.MoveFileAsync(replacedFileId, StorageFolderConstant.Archived, StorageFolderConstant.Approved,
                                replacedDocument.DepartmentId, replacedApprovedVersion.IsPublic);
                        }
                    }

                    // Rollback previous version if it was moved
                    if (previousApprovedVersion != null)
                    {
                        var previousFileId = previousApprovedVersion.GoogleDriveFileId ?? previousApprovedVersion.FilePath;
                        await _storageService.MoveFileAsync(previousFileId, StorageFolderConstant.Archived, StorageFolderConstant.Approved,
                            previousApprovedVersion.DocumentFile.DepartmentId, previousApprovedVersion.IsPublic);
                    }

                    // Rollback current document
                    var currentFileId = versionToReview.GoogleDriveFileId ?? versionToReview.FilePath;
                    await _storageService.MoveFileAsync(currentFileId, StorageFolderConstant.Approved, StorageFolderConstant.Pending,
                        versionToReview.DocumentFile.DepartmentId, versionToReview.IsPublic);

                    throw;
                }
            }
            else
            {
                // ========================================
                // DOCUMENT REJECTION HANDLING
                // ========================================
                // Validate rejection comments and update status
                if (string.IsNullOrWhiteSpace(request.Comments) || request.Comments.Length < 10)
                    throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST,  MessageConstant.CommentsRequiredForRejection);
                versionToReview.Status = StatusEnum.Rejected;
                logAction = ApprovalAction.Reject;
            }

            // ========================================
            // FINALIZE DATABASE CHANGES
            // ========================================
            // Update document metadata and save all changes
            documentFile.LastUpdatedBy = userId;
            documentFile.LastUpdatedTime = DateTime.UtcNow;
            await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(versionToReview);

            var approvalLog = new ApprovalLog
            {
                Action = logAction,
                Comments = request.Comments,
                CreatedBy = userId,
                DocumentVersionId = versionToReview.Id,
            };
            await _unitOfWork.GetRepository<ApprovalLog>().InsertAsync(approvalLog);

            var activeClaim = await _unitOfWork.GetRepository<ApprovalClaim>()
                .SingleOrDefaultAsync(predicate: ac => ac.DocumentVersionId == versionToReview.Id && ac.IsActive);
            if (activeClaim != null)
            {
                activeClaim.IsActive = false;
                activeClaim.LastUpdatedBy = userId;
                activeClaim.LastUpdatedTime = DateTime.UtcNow;
                await _unitOfWork.GetRepository<ApprovalClaim>().UpdateAsync(activeClaim);
            }

            await _unitOfWork.CommitAsync();

            // ========================================
            // GOOGLE DRIVE PERMISSIONS UPDATE
            // ========================================
            // Update file permissions based on the new document status
            try
            {
                var fileId = versionToReview.GoogleDriveFileId ?? versionToReview.FilePath;
                var newStatus = request.IsApproved ? StatusEnum.Approved : StatusEnum.Rejected;

                await _permissionManager.UpdateDocumentPermissionsAsync(
                    fileId,
                    StatusEnum.Pending,
                    newStatus,
                    versionToReview.DocumentFile.DepartmentId,
                    versionToReview.IsPublic,
                    versionToReview.DocumentFile.OwnerId);

                _logger.LogInformation("Updated permissions for document {VersionId} from Pending to {NewStatus}", versionId, newStatus);

                // If this was an approval, update permissions for archived documents
                if (request.IsApproved)
                {
                    // Update permissions for previous version that got archived
                    var archivedVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                        .SingleOrDefaultAsync(predicate: v => v.DocumentFileId == versionToReview.DocumentFileId && v.Status == StatusEnum.Archived && v.Id != versionToReview.Id,
                                            include: i => i.Include(v => v.DocumentFile));

                    if (archivedVersion != null)
                    {
                        var archivedFileId = archivedVersion.GoogleDriveFileId ?? archivedVersion.FilePath;
                        await _permissionManager.UpdateDocumentPermissionsAsync(
                            archivedFileId,
                            StatusEnum.Approved,
                            StatusEnum.Archived,
                            archivedVersion.DocumentFile.DepartmentId,
                            archivedVersion.IsPublic,
                            archivedVersion.DocumentFile.OwnerId);

                        _logger.LogInformation("Updated permissions for archived document {ArchivedVersionId} from Approved to Archived", archivedVersion.Id);
                    }

                    // Update permissions for replaced document that got archived
                    if (replacedDocument != null)
                    {
                        var replacedApprovedVersion = replacedDocument.DocumentVersions.FirstOrDefault(v => v.Status == StatusEnum.Archived);
                        if (replacedApprovedVersion != null)
                        {
                            var replacedFileId = replacedApprovedVersion.GoogleDriveFileId ?? replacedApprovedVersion.FilePath;
                            await _permissionManager.UpdateDocumentPermissionsAsync(
                                replacedFileId,
                                StatusEnum.Approved,
                                StatusEnum.Archived,
                                replacedDocument.DepartmentId,
                                replacedApprovedVersion.IsPublic,
                                replacedDocument.OwnerId);

                            _logger.LogInformation("Updated permissions for replaced document {ReplacedDocumentId} from Approved to Archived", replacedDocument.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update permissions for document {VersionId} during review", versionId);
                // Don't fail the entire operation for permission errors
            }

            _logger.LogInformation("Manager {UserId} has {Action} document version {VersionId}", userId, logAction, versionId);

            // ========================================
            // NOTIFICATION SYSTEM
            // ========================================
            // Send notifications to document owner and department users
            try
            {
                var currentUser = _httpContextAccessor.HttpContext?.User;
                if (currentUser != null)
                {
                    if (request.IsApproved)
                    {
                        // 1. Send notification to document owner
                        var ownerEmail = await GetUserEmailByIdAsync(versionToReview.DocumentFile.OwnerId);
                        var ownerName = await GetUserNameByIdAsync(versionToReview.DocumentFile.OwnerId);

                        if (!string.IsNullOrEmpty(ownerEmail))
                        {
                            await _notificationService.SendDocumentApprovalNotificationAsync(
                                versionId,
                                versionToReview.Title,
                                versionToReview.VersionName,
                                ownerEmail,
                                ownerName ?? "Document Owner",
                                currentUser,
                                request.Comments);
                            _logger.LogInformation("Document approval notification sent to owner for document {VersionId}", versionId);
                        }
                        else
                        {
                            _logger.LogWarning("Could not find owner email for document {VersionId}, owner ID: {OwnerId}", versionId, versionToReview.DocumentFile.OwnerId);
                        }

                        // 2. Send department-wide publication notification
                        var documentTags = versionToReview.DocumentTags?.Select(dt => dt.Tag.Name).ToList() ?? new List<string>();
                        await _notificationService.SendDocumentPublicationNotificationAsync(
                            versionId,
                            versionToReview.Title,
                            versionToReview.VersionName,
                            currentUser,
                            versionToReview.DocumentFile.DepartmentId,
                            versionToReview.IsPublic,
                            versionToReview.DocumentFile.DocumentTypeId,
                            versionToReview.EffectiveFrom,
                            versionToReview.EffectiveUntil,
                            documentTags);
                        _logger.LogInformation("Document publication notification sent to department for document {VersionId}", versionId);
                    }
                    else
                    {
                        // Send rejection notification to document owner
                        var ownerEmail = await GetUserEmailByIdAsync(versionToReview.DocumentFile.OwnerId);
                        var ownerName = await GetUserNameByIdAsync(versionToReview.DocumentFile.OwnerId);

                        if (!string.IsNullOrEmpty(ownerEmail))
                        {
                            await _notificationService.SendDocumentRejectionNotificationAsync(
                                versionId,
                                versionToReview.Title,
                                versionToReview.VersionName,
                                ownerEmail,
                                ownerName ?? "Document Owner",
                                currentUser,
                                request.Comments ?? "No comments provided");
                            _logger.LogInformation("Document rejection notification sent for document {VersionId}", versionId);
                        }
                        else
                        {
                            _logger.LogWarning("Could not find owner email for document {VersionId}, owner ID: {OwnerId}", versionId, versionToReview.DocumentFile.OwnerId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications for document {VersionId}", versionId);
                // Don't fail the entire operation for notification errors
            }
        }

        public async Task SubmitForApprovalAsync(string versionId)
        {
            // Get current user ID from JWT token
            var userId = GetCurrentUserId();

            //1. Get the document
            var version = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                predicate: v => v.Id == versionId,
                include: i => i.Include(v =>v.DocumentFile).ThenInclude(df => df.DocumentType)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFoundDetailed);
            //2.Check owner ID
            if (version.DocumentFile.OwnerId != userId)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToSubmit);
            }
            //3. Check if the version status 
            if (version.Status != StatusEnum.Draft)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.CannotSubmitForApproval, version.Status));
            }

            version.Status = StatusEnum.Pending; // Update status to Pending
            version.LastUpdatedBy = "system"; // temp
            version.LastUpdatedTime = DateTime.UtcNow; // Update timestamp

            //4. Move the document file to the "Pending" folder in Google Drive
            var fileId = version.GoogleDriveFileId ?? version.FilePath;
            await _storageService.MoveFileAsync(fileId, StorageFolderConstant.Drafts, StorageFolderConstant.Pending,
                version.DocumentFile.DepartmentId, version.IsPublic);
            // FilePath remains the Google Drive file ID - no change needed

            //5. Change the file path to point to the new location

            //6. Save changes to the database
            await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(version);
            await _unitOfWork.CommitAsync();

            //7. Update Google Drive permissions (Draft -> Pending: owner + department managers)
            try
            {
                await _permissionManager.UpdateDocumentPermissionsAsync(
                    fileId,
                    StatusEnum.Draft,
                    StatusEnum.Pending,
                    version.DocumentFile.DepartmentId,
                    version.IsPublic,
                    userId);
                _logger.LogInformation("Updated permissions for document {VersionId} from Draft to Pending", versionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update permissions for document {VersionId} when submitting for approval", versionId);
                // Don't fail the entire operation for permission errors
            }

            //8. Send notification to department managers
            try
            {
                var currentUser = _httpContextAccessor.HttpContext?.User;
                if (currentUser != null)
                {
                    await _notificationService.SendDocumentSubmissionNotificationAsync(
                        versionId,
                        version.Title,
                        version.VersionName,
                        currentUser,
                        version.DocumentFile.DepartmentId);
                    _logger.LogInformation("Document submission notification sent for document {VersionId}", versionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send submission notification for document {VersionId}", versionId);
                // Don't fail the entire operation for notification errors
            }
        }

        private string GetCurrentUserId()
        {
            return JwtTokenHelper.GetUserId(_httpContextAccessor);
        }

        private string? GetCurrentUserDepartmentId()
        {
            return JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);
        }

        #region Helper Methods for Notifications

        /// <summary>
        /// Get user email by ID from Auth service via MassTransit
        /// </summary>
        private async Task<string?> GetUserEmailByIdAsync(string userId)
        {
            try
            {
                // This would typically use a request client to Auth service
                // For now, we'll use the permission manager's existing functionality
                // In a real implementation, you might want to add a dedicated method
                _logger.LogInformation("Getting user email for user ID: {UserId}", userId);

                // TODO: Implement proper user lookup via MassTransit
                // For now, return a placeholder that indicates we need the email
                return $"user-{userId}@company.com"; // Placeholder - should be replaced with actual lookup
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user email for user ID: {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Get user name by ID from Auth service via MassTransit
        /// </summary>
        private async Task<string?> GetUserNameByIdAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Getting user name for user ID: {UserId}", userId);

                // TODO: Implement proper user lookup via MassTransit
                // For now, return a placeholder
                return $"User {userId}"; // Placeholder - should be replaced with actual lookup
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user name for user ID: {UserId}", userId);
                return null;
            }
        }

        #endregion
    }
}
