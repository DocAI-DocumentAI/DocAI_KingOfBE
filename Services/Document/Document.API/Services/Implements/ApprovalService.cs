using AutoMapper;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Payload.Response.Folder; // ✅ FOLDER-AWARE: For FolderSummaryResponse
using Document.API.Services.Interfaces;
using static Document.API.Services.Interfaces.IFolderAwareApprovalService; // ✅ FOLDER-AWARE: For ApprovalReviewResponse
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
using System.Linq.Expressions;
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

        private readonly INameLookupService _nameLookupService;

        public ApprovalService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<ApprovalService> logger, IStorageService storageService, IKernelMemory kernelMemory, IDocumentEnrichmentService enrichmentService, IDocumentPermissionManager permissionManager, IDocumentNotificationService notificationService, IHttpContextAccessor httpContextAccessor, INameLookupService nameLookupService)
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
            _nameLookupService = nameLookupService;
        }
        /// <summary>
        /// Enhanced approval queue with comprehensive filtering and summary statistics
        /// </summary>
        public async Task<ApprovalQueueSummaryResponse> GetApprovalQueueAsync(Document.Infrastructure.Filter.ApprovalQueueFilter filter, int pageNumber, int pageSize)
        {
            var departmentId = GetCurrentUserDepartmentId() ?? throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessApprovalQueue);

            // Enhanced predicate to support all status types for filtering
            Expression<Func<DocumentVersion, bool>> basePredicate = v => v.DocumentFile.DepartmentId == departmentId;

            // If no status filter is specified, default to pending and rejected (original behavior)
            if (!filter.Status.HasValue)
            {
                basePredicate = v => v.DocumentFile.DepartmentId == departmentId &&
                                   (v.Status == StatusEnum.Pending || v.Status == StatusEnum.Rejected);
            }

            // Store ReviewedBy filter value and temporarily remove it from filter to handle separately
            var reviewedByFilter = filter.ReviewedBy;
            filter.ReviewedBy = null; // Temporarily remove to avoid filter expression issues

            // Get paginated documents with enhanced includes
            var documentVersions = await _unitOfWork.GetRepository<DocumentVersion>()
                .GetPagingListAsync(
                selector: v => v,
                filter: filter,
                include: i => i.Include(v => v.DocumentFile)
                              .ThenInclude(df => df.DocumentType!)
                              .Include(v => v.DocumentFile)
                              .ThenInclude(df => df.ReplacementDocument)
                              .Include(v => v.ApprovalClaim!)
                              .Include(v => v.DocumentTags)
                              .ThenInclude(dt => dt.Tag)
                              .Include(v => v.Folder!)
                              .Include(v => v.TargetFolder!),
                predicate: basePredicate,
                orderBy: v => v.OrderBy(v => v.LastSubmitted),
                page: pageNumber,
                size: pageSize
                );

            // Restore the ReviewedBy filter value
            filter.ReviewedBy = reviewedByFilter;

            // Apply ReviewedBy filter if specified (filter by ApprovalLog.CreatedBy)
            if (!string.IsNullOrEmpty(reviewedByFilter))
            {
                // Get document version IDs that have approval logs created by the specified reviewer
                var reviewedDocumentIds = await _unitOfWork.GetRepository<ApprovalLog>()
                    .GetListAsync(
                        predicate: log => log.CreatedBy == reviewedByFilter,
                        selector: log => log.DocumentVersionId
                    );

                // Filter the documents to only include those reviewed by the specified user
                var filteredItems = documentVersions.Items.Where(doc => reviewedDocumentIds.Contains(doc.Id)).ToList();

                // Update the paginated result
                documentVersions = new Paginate<DocumentVersion>
                {
                    Items = filteredItems,
                    Page = documentVersions.Page,
                    Size = documentVersions.Size,
                    Total = filteredItems.Count, // Update total to reflect filtered count
                    TotalPages = (int)Math.Ceiling((double)filteredItems.Count / documentVersions.Size)
                };
            }

            // Map to enhanced response objects with additional fields
            var pendingDocuments = new Paginate<PendingDocumentResponse>
            {
                Items = documentVersions.Items.Select(v => MapToEnhancedPendingDocumentResponse(v)).ToList(),
                Page = documentVersions.Page,
                Size = documentVersions.Size,
                Total = documentVersions.Total,
                TotalPages = documentVersions.TotalPages
            };

            // Enrich with names
            var enrichedDocuments = await _enrichmentService.EnrichPendingDocumentResponsesAsync(pendingDocuments.Items.ToList());

            // Reverse replacement relationships are now populated directly from database via ReplacedById field

            // Add claim and additional information
            foreach (var document in enrichedDocuments)
            {
                var originalVersion = documentVersions.Items.FirstOrDefault(v => v.Id == document.VersionId);
                if (originalVersion != null)
                {
                    // Claim information
                    if (originalVersion.ApprovalClaim != null && originalVersion.ApprovalClaim.IsActive)
                    {
                        document.IsBeingReviewed = true;
                        document.ReviewedBy = originalVersion.ApprovalClaim.ClaimedBy;
                        document.ClaimedAt = originalVersion.ApprovalClaim.ClaimedAt;
                    }

                    // Calculate additional fields
                    document.DaysSinceSubmission = originalVersion.LastSubmitted.HasValue
                        ? (DateTime.UtcNow - originalVersion.LastSubmitted.Value).Days
                        : 0;

                    document.IsApproachingExpiration = document.DaysSinceSubmission >= 5; // 5+ days approaching 7-day limit

                    document.Priority = CalculatePriority(originalVersion);
                }
            }

            // Calculate summary statistics
            var statistics = await CalculateApprovalQueueStatisticsAsync(departmentId);

            // Create final paginated result
            var finalPaginated = new Paginate<PendingDocumentResponse>
            {
                Items = enrichedDocuments,
                Page = pendingDocuments.Page,
                Size = pendingDocuments.Size,
                Total = pendingDocuments.Total,
                TotalPages = pendingDocuments.TotalPages
            };

            var response = new ApprovalQueueSummaryResponse
            {
                Documents = finalPaginated,
                Statistics = statistics
            };

            _logger.LogInformation("Retrieved approval queue with {Count} documents and statistics for department {DepartmentId}",
                enrichedDocuments.Count, departmentId);

            return response;
        }

        /// <summary>
        /// Backward compatibility method - returns only the paginated documents without statistics
        /// </summary>
        public async Task<IPaginate<PendingDocumentResponse>> GetApprovalQueueLegacyAsync(Document.Infrastructure.Filter.ApprovalQueueFilter filter, int pageNumber, int pageSize)
        {
            var enhancedResponse = await GetApprovalQueueAsync(filter, pageNumber, pageSize);
            return enhancedResponse.Documents;
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

            await _unitOfWork.GetRepository<ApprovalClaim>().UpdateAsync(existingClaim);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Document version {VersionId} claim released by user {UserId}", versionId, userId);
        }

        public async Task KeepClaimAliveAsync(string versionId)
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
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToKeepClaimAlive);
            }

            // Update the LastUpdatedTime to keep the claim alive
            existingClaim.LastUpdatedBy = userId;
            existingClaim.LastUpdatedTime = DateTime.UtcNow;

            await _unitOfWork.GetRepository<ApprovalClaim>().UpdateAsync(existingClaim);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Document version {VersionId} claim kept alive by user {UserId}", versionId, userId);
        }

        public async Task<ApprovalQueueDetailResponse> GetApprovalQueueDetailAsync(string versionId)
        {
            // Get current manager's department ID from JWT token
            var managerDepartmentId = GetCurrentUserDepartmentId();

            var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId && (v.Status == StatusEnum.Pending || v.Status == StatusEnum.Rejected),
                    include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType!).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag).Include(v => v.ApprovalClaim!)
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

            // Set calculated fields
            response.DaysSinceSubmission = documentVersion.LastSubmitted.HasValue
                ? (DateTime.UtcNow - documentVersion.LastSubmitted.Value).Days
                : 0;

            response.IsApproachingExpiration = response.DaysSinceSubmission >= 5;
            response.Priority = CalculatePriority(documentVersion);

            // Set additional fields that might not be in the mapper
            response.ResubmissionCount = 0; // TODO: Implement resubmission tracking
            response.DownloadCount = 0; // TODO: Implement download tracking
            response.ViewCount = 0; // TODO: Implement view tracking

            var enrichedResponse = await _enrichmentService.EnrichApprovalQueueDetailResponseAsync(response);

            _logger.LogInformation("Enriched approval queue detail response with names for version {VersionId}", versionId);
            return enrichedResponse;
        }

        /// <summary>
        /// ✅ FOLDER-AWARE: Review document (approve/reject) with folder-aware logic and complete Kernel Memory integration
        /// </summary>
        public async Task<ApprovalReviewResponse> ReviewDocument(string versionId, ReviewDocumentRequest request)
        {
            // Get current user ID and department ID from JWT token
            var userId = GetCurrentUserId();
            var managerDepartmentId = GetCurrentUserDepartmentId();

            var versionToReview = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(
                predicate: v => v.Id == versionId,
                include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType!)
                              .Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                              .Include(v => v.Folder!) // ✅ FOLDER-AWARE: Include current folder information
                              .Include(v => v.TargetFolder!) // ✅ FOLDER-AWARE: Include target folder information
            ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);
            var documentFile = versionToReview.DocumentFile;

            // Declare variables at method level for broader scope
            DocumentFile? replacedDocument = null;

            // --- Permission and State Validation ---
            if (documentFile.DepartmentId != managerDepartmentId)
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessApprovalQueue);

            if (versionToReview.Status != StatusEnum.Pending)
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.NotPendingApproval, versionToReview.Status));

            // BR-221: Check if document is claimed by another manager
            // COMMENTED OUT: Claim check for review - allowing direct approval/rejection without claiming
            /*
            var existingClaim = await _unitOfWork.GetRepository<ApprovalClaim>()
                .SingleOrDefaultAsync(predicate: ac => ac.DocumentVersionId == versionId && ac.IsActive);

            if (existingClaim != null && existingClaim.ClaimedBy != userId)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                    string.Format(MessageConstant.DocumentAlreadyClaimed, existingClaim.ClaimedBy));
            }

            // If not claimed by current user, they must claim it first
            if (existingClaim == null)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST,
                    "Document must be claimed for review before approval/rejection actions can be taken (BR-221)");
            }
            */

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
                    // Load the document being replaced regardless of its current IsReplaced flag.
                    // BR-037 sets IsReplaced at submission to block concurrent replacements,
                    // but at approval time we must still archive its latest approved version.
                    replacedDocument = await _unitOfWork.GetRepository<DocumentFile>()
                        .SingleOrDefaultWithTrackingAsync(
                            predicate: df => df.Id == documentFile.ReplacementId,
                            include: i => i.Include(df => df.DocumentVersions.Where(v => v.Status == StatusEnum.Approved))
                                          .ThenInclude(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                        );
                }

                try
                {
                    // ========================================
                    // SCENARIO 3: DOCUMENT REPLACEMENT HANDLING
                    // ========================================
                    // If this document replaces another document, handle the replacement logic
                    if (replacedDocument != null)
                    {
                        var replacedApprovedVersion = replacedDocument.DocumentVersions.FirstOrDefault(v => v.Status == StatusEnum.Approved);
                        if (replacedApprovedVersion != null)
                        {
                            // ✅ NEW LOGIC: Check if replaced document still has valid effective date
                            bool shouldArchiveReplacedDocument = true;

                            if (replacedApprovedVersion.EffectiveUntil.HasValue)
                            {
                                var currentDate = DateTime.UtcNow.Date;
                                var effectiveUntilDate = replacedApprovedVersion.EffectiveUntil.Value.Date;

                                if (currentDate <= effectiveUntilDate)
                                {
                                    // Document still has valid effective date, just mark as replaced but don't archive
                                    shouldArchiveReplacedDocument = false;
                                    _logger.LogInformation("Replaced document {ReplacedDocumentId} still has valid effective date until {EffectiveUntil}, marking as replaced only",
                                        replacedDocument.Id, effectiveUntilDate);
                                }
                            }

                            if (shouldArchiveReplacedDocument)
                            {
                                // ✅ ARCHIVE LOGIC: Archive replaced document in-place (no folder movement)
                                // Documents are archived by status change, not by moving to archive folders
                                var replacedFileId = replacedApprovedVersion.GoogleDriveFileId ?? replacedApprovedVersion.FilePath;
                                _logger.LogInformation("Archiving replaced document {FileId} in-place (no folder movement required)", replacedFileId);
                                // No file movement needed - document stays in its functional folder but status changes to archived

                                // Remove replaced document from Kernel Memory instead of archiving its embeddings
                                var replacedVersionKmId = replacedApprovedVersion.Id.ToString();
                                try
                                {
                                    await _memory.DeleteDocumentAsync(replacedVersionKmId).WaitAsync(TimeSpan.FromSeconds(10));
                                    _logger.LogInformation("Removed replaced version {VersionId} from Kernel Memory.", replacedVersionKmId);
                                }
                                catch (TimeoutException)
                                {
                                    _logger.LogWarning("Timeout removing replaced version {VersionId} from Kernel Memory", replacedVersionKmId);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to remove replaced version {VersionId} from Kernel Memory", replacedVersionKmId);
                                }

                                // Update database - mark replaced document as archived
                                replacedApprovedVersion.Status = StatusEnum.Archived;
                                replacedApprovedVersion.IsOfficial = false;
                                await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(replacedApprovedVersion);

                                _logger.LogInformation("Archived replaced document {ReplacedDocumentId} and removed from Kernel Memory.", replacedDocument.Id);
                            }
                            else
                            {
                                // ✅ REPLACEMENT ONLY: Document still effective, just mark as replaced
                                _logger.LogInformation("Replaced document {ReplacedDocumentId} still effective until {EffectiveUntil}, keeping active status",
                                    replacedDocument.Id, replacedApprovedVersion.EffectiveUntil);
                            }

                            // ✅ ALWAYS UPDATE: Mark the DocumentFile as replaced regardless of archiving
                            replacedDocument.IsReplaced = true;
                            replacedDocument.ReplacedById = documentFile.Id; // Set reverse relationship
                            await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(replacedDocument);
                        }
                    }

                    // ========================================
                    // SCENARIO 2: VERSION ARCHIVING HANDLING
                    // ========================================
                    // If there's a previous approved version of the SAME document, archive it
                    if (previousApprovedVersion != null)
                    {
                        // ✅ NEW FOLDER DESIGN: Archive previous version in-place (no folder movement)
                        // Previous versions are archived by status change, not by moving to archive folders
                        var previousFileId = previousApprovedVersion.GoogleDriveFileId ?? previousApprovedVersion.FilePath;
                        _logger.LogInformation("Archiving previous version {FileId} in-place (no folder movement required)", previousFileId);
                        // No file movement needed - document stays in its functional folder but status changes to archived
                        // FilePath remains the Google Drive file ID - no change needed

                        // Remove previous approved version embeddings immediately (no archiving)
                        var previousVersionKmIdEarly = previousApprovedVersion.Id.ToString();
                        try
                        {
                            await _memory.DeleteDocumentAsync(previousVersionKmIdEarly).WaitAsync(TimeSpan.FromSeconds(10));
                            _logger.LogInformation("Removed previous approved version {VersionId} from Kernel Memory (early).", previousVersionKmIdEarly);
                        }
                        catch (TimeoutException)
                        {
                            _logger.LogWarning("Timeout removing previous approved version {VersionId} from Kernel Memory (early)", previousVersionKmIdEarly);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to remove previous approved version {VersionId} from Kernel Memory (early)", previousVersionKmIdEarly);
                        }
                    }

                    // ========================================
                    // CURRENT DOCUMENT APPROVAL
                    // ========================================
                    // ✅ FOLDER-AWARE: Move document from drafts to target functional folder
                    // Documents stay in drafts during pending status, then move to functional folders when approved
                    var currentFileId = versionToReview.GoogleDriveFileId ?? versionToReview.FilePath;

                    // ✅ FOLDER-AWARE: Determine target folder for approved document
                    string? targetFolderId = null;

                    // 1. First priority: TargetFolderId from request
                    if (!string.IsNullOrEmpty(request.TargetFolderId))
                    {
                        targetFolderId = request.TargetFolderId;
                        _logger.LogInformation("Using target folder from request: {TargetFolderId}", targetFolderId);
                    }
                    // 2. Second priority: TargetFolderId from the version being submitted (stored during draft creation)
                    else if (!string.IsNullOrEmpty(versionToReview.TargetFolderId))
                    {
                        targetFolderId = versionToReview.TargetFolderId;
                        _logger.LogInformation("Using target folder from document version: {TargetFolderId}", targetFolderId);
                    }
                    // 3. Last resort: Department root folder
                    else
                    {
                        targetFolderId = await GetDepartmentRootFolderAsync(documentFile.DepartmentId);
                        _logger.LogInformation("Using department root folder as fallback: {FolderId}", targetFolderId);
                    }

                    if (!string.IsNullOrEmpty(targetFolderId))
                    {
                        // Get target folder information
                        var targetFolder = await _unitOfWork.GetRepository<Folder>()
                            .SingleOrDefaultAsync(predicate: f => f.Id == targetFolderId);

                        if (targetFolder != null && !string.IsNullOrEmpty(targetFolder.GoogleDriveFolderId))
                        {
                            // Move file to target functional folder in Google Drive
                            await _storageService.MoveFileToFolderAsync(currentFileId, targetFolder.GoogleDriveFolderId);
                            _logger.LogInformation("Moved approved document {FileId} to functional folder {FolderName}",
                                currentFileId, targetFolder.Name);

                            // ✅ FOLDER-AWARE: Update folder ID in database
                            versionToReview.FolderId = targetFolderId;
                        }
                        else
                        {
                            _logger.LogWarning("Target folder {FolderId} not found or missing Google Drive ID", targetFolderId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No target folder could be determined for approved document {DocumentId}", documentFile.Id);
                    }
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

                        // Remove previous approved version embeddings instead of archiving in Kernel Memory
                        try
                        {
                            await _memory.DeleteDocumentAsync(previousVersionKmId).WaitAsync(TimeSpan.FromSeconds(10));
                            _logger.LogInformation("Removed previous approved version {VersionId} from Kernel Memory.", previousVersionKmId);
                        }
                        catch (TimeoutException)
                        {
                            _logger.LogWarning("Timeout removing previous approved version {VersionId} from Kernel Memory", previousVersionKmId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to remove previous approved version {VersionId} from Kernel Memory", previousVersionKmId);
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
                    logAction = ApprovalAction.Approved;

                    // ========================================
                    // KERNEL MEMORY INDEXING
                    // ========================================
                    // Index the approved document in Kernel Memory with complete metadata
                    var tags = new TagCollection
                    {
                        // Core identifiers
                        { SemanticSearchConstant.MemoryTags.Status, "approved" },
                        { SemanticSearchConstant.MemoryTags.DocumentId, documentFile.Id.ToString() },
                        { SemanticSearchConstant.MemoryTags.DepartmentId, documentFile.DepartmentId },
                        { SemanticSearchConstant.MemoryTags.OwnerId, documentFile.OwnerId },
                        { SemanticSearchConstant.MemoryTags.Version, versionToReview.VersionName },
                        { SemanticSearchConstant.MemoryTags.IsOfficial, versionToReview.IsOfficial.ToString().ToLower() },
                        { SemanticSearchConstant.MemoryTags.IsPublic, versionToReview.IsPublic.ToString() },
                        { SemanticSearchConstant.MemoryTags.ApprovalDate, DateTime.UtcNow.ToString("yyyy-MM-dd") },
                        { SemanticSearchConstant.MemoryTags.CreatedBy, versionToReview.CreatedBy },
                        { SemanticSearchConstant.MemoryTags.SubmittedBy, versionToReview.SubmittedBy ?? versionToReview.CreatedBy },
                        { SemanticSearchConstant.MemoryTags.LastSubmitted, versionToReview.LastSubmitted?.ToString("o") },

                        // Document core metadata
                        { SemanticSearchConstant.MemoryTags.Title, documentFile.Title },
                        { SemanticSearchConstant.MemoryTags.Description, documentFile.Description },
                        { SemanticSearchConstant.MemoryTags.VersionTitle, versionToReview.Title },
                        { SemanticSearchConstant.MemoryTags.Summary, versionToReview.Summary },
                        { SemanticSearchConstant.MemoryTags.DocumentType, versionToReview.DocumentFile.DocumentTypeId },
                        { SemanticSearchConstant.MemoryTags.SignedBy, versionToReview.SignedBy },
                        { SemanticSearchConstant.MemoryTags.EffectiveFrom, versionToReview.EffectiveFrom?.ToString("yyyy-MM-dd") },
                        { SemanticSearchConstant.MemoryTags.EffectiveUntil, versionToReview.EffectiveUntil?.ToString("yyyy-MM-dd") },

                        // File system metadata
                        { SemanticSearchConstant.MemoryTags.FileName, versionToReview.FileName },
                        { SemanticSearchConstant.MemoryTags.FileType, versionToReview.FileType },
                        { SemanticSearchConstant.MemoryTags.FileSize, versionToReview.FileSize.ToString() },
                        { SemanticSearchConstant.MemoryTags.FileHash, versionToReview.FileHash },
                        { SemanticSearchConstant.MemoryTags.GoogleDriveFileId, versionToReview.GoogleDriveFileId ?? versionToReview.FilePath },
                        { SemanticSearchConstant.MemoryTags.StorageLocation, "GoogleDrive" }
                    };

                    // Classification: tags and document type details
                    if (versionToReview.DocumentTags != null)
                    {
                        foreach (var docTag in versionToReview.DocumentTags)
                        {
                            if (!string.IsNullOrWhiteSpace(docTag.Tag?.Name))
                                tags.Add(SemanticSearchConstant.MemoryTags.Tags, docTag.Tag.Name);
                        }
                    }

                    // Add document type friendly name/description if available
                    if (versionToReview.DocumentFile.DocumentType != null)
                    {
                        tags.Add(SemanticSearchConstant.MemoryTags.DocumentTypeName, versionToReview.DocumentFile.DocumentType.Name);
                        if (!string.IsNullOrWhiteSpace(versionToReview.DocumentFile.DocumentType.Description))
                            tags.Add(SemanticSearchConstant.MemoryTags.DocumentTypeDescription, versionToReview.DocumentFile.DocumentType.Description);
                    }

                    // Organizational metadata (names via NameLookupService)
                    try
                    {
                        var ownerName = await _nameLookupService.GetUserNameAsync(documentFile.OwnerId);
                        if (!string.IsNullOrWhiteSpace(ownerName))
                            tags.Add(SemanticSearchConstant.MemoryTags.OwnerName, ownerName);

                        var deptName = await _nameLookupService.GetDepartmentNameAsync(documentFile.DepartmentId);
                        if (!string.IsNullOrWhiteSpace(deptName))
                            tags.Add(SemanticSearchConstant.MemoryTags.DepartmentName, deptName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to enrich organizational metadata for indexing");
                    }

                    // Relationship metadata: previous approved version
                    if (previousApprovedVersion != null)
                    {
                        tags.Add(SemanticSearchConstant.MemoryTags.PreviousApprovedVersionId, previousApprovedVersion.Id);
                        tags.Add(SemanticSearchConstant.MemoryTags.PreviousApprovedVersionName, previousApprovedVersion.VersionName);
                        tags.Add(SemanticSearchConstant.MemoryTags.PreviousApprovedAt, previousApprovedVersion.CreatedTime.ToString("o"));
                    }

                    // Relationship metadata: replacement
                    if (!string.IsNullOrEmpty(documentFile.ReplacementId))
                    {
                        tags.Add(SemanticSearchConstant.MemoryTags.ReplacementOfDocumentId, documentFile.ReplacementId);
                    }
                    if (replacedDocument != null)
                    {
                        tags.Add(SemanticSearchConstant.MemoryTags.ReplacedDocumentId, replacedDocument.Id);
                    }

                    // Access control metadata
                    tags.Add(SemanticSearchConstant.MemoryTags.Visibility, versionToReview.IsPublic ? "public" : "department");
                    tags.Add(SemanticSearchConstant.MemoryTags.DepartmentRestriction, versionToReview.IsPublic ? "none" : documentFile.DepartmentId);
                    // Optional: permission level placeholder (readers via Drive, editors via company account)
                    tags.Add(SemanticSearchConstant.MemoryTags.PermissionLevel, versionToReview.IsPublic ? "company-read" : "department-read");

                    // ✅ FOLDER-AWARE: Add folder metadata for enhanced search and organization
                    if (versionToReview.Folder != null)
                    {
                        tags.Add(SemanticSearchConstant.MemoryTags.FolderId, versionToReview.Folder.Id);
                        tags.Add(SemanticSearchConstant.MemoryTags.FolderName, versionToReview.Folder.Name);
                        if (!string.IsNullOrWhiteSpace(versionToReview.Folder.FullPath))
                            tags.Add(SemanticSearchConstant.MemoryTags.FolderPath, versionToReview.Folder.FullPath);
                        if (!string.IsNullOrWhiteSpace(versionToReview.Folder.Description))
                            tags.Add(SemanticSearchConstant.MemoryTags.FolderDescription, versionToReview.Folder.Description);
                        tags.Add(SemanticSearchConstant.MemoryTags.FolderIsPublic, versionToReview.Folder.IsPublic.ToString().ToLower());
                    }

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
                        using var kmCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                        await _memory.ImportDocumentAsync(fileStream, versionToReview.FileName, documentId: versionKmId, tags: tags, cancellationToken: kmCts.Token);
                    }
                    _logger.LogInformation("Indexed approved version {VersionId} in Kernel Memory with structured tags.", versionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the approval process for version {VersionId}. Reverting storage changes.", versionId);

                    // ✅ NEW FOLDER DESIGN: No file movement rollback needed
                    // Documents are archived in-place and current document stays in drafts until approved
                    _logger.LogInformation("No file movement rollback needed - documents remain in their original locations");

                    // Rollback replaced document status (no file movement)
                    if (replacedDocument != null)
                    {
                        var replacedApprovedVersion = replacedDocument.DocumentVersions.FirstOrDefault(v => v.Status == StatusEnum.Approved);
                        if (replacedApprovedVersion != null)
                        {
                            var replacedFileId = replacedApprovedVersion.GoogleDriveFileId ?? replacedApprovedVersion.FilePath;
                            _logger.LogInformation("Rollback: Replaced document {FileId} status will be reverted by database rollback", replacedFileId);
                        }
                    }

                    // Rollback previous version status (no file movement)
                    if (previousApprovedVersion != null)
                    {
                        var previousFileId = previousApprovedVersion.GoogleDriveFileId ?? previousApprovedVersion.FilePath;
                        _logger.LogInformation("Rollback: Previous version {FileId} status will be reverted by database rollback", previousFileId);
                    }

                    // Rollback current document (no file movement needed - stays in drafts)
                    var currentFileId = versionToReview.GoogleDriveFileId ?? versionToReview.FilePath;
                    _logger.LogInformation("Rollback: Current document {FileId} remains in drafts folder", currentFileId);

                    throw;
                }
            }
            else
            {
                // ========================================
                // DOCUMENT REJECTION HANDLING
                // ========================================
                // BR-226: Comments are mandatory when a document is rejected
                // BR-232: Rejection comments must be at least 10 characters long
                if (string.IsNullOrWhiteSpace(request.Comments))
                    throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.CommentsRequiredForRejection);

                if (request.Comments.Trim().Length < 10)
                    throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, "Rejection comments must be at least 10 characters long (BR-232)");

                versionToReview.Status = StatusEnum.Rejected;
                logAction = ApprovalAction.Rejected;

                _logger.LogInformation("Document {VersionId} rejected with comments: {Comments}", versionId, request.Comments);
            }

            // ========================================
            // FINALIZE DATABASE CHANGES
            // ========================================
            // Update document metadata and save all changes
            documentFile.LastUpdatedBy = userId;
            documentFile.LastUpdatedTime = DateTime.UtcNow;
            // Persist DocumentFile metadata separately to avoid EF graph tracking conflicts
            await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentFile);
            await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(versionToReview);

            var approvalLog = new ApprovalLog
            {
                Action = logAction,
                Comments = request.Comments,
                CreatedBy = userId,
                LastUpdatedBy = userId,
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

            // BR-217: Document is now removed from approval queue for all managers due to status change
            _logger.LogInformation("Document {VersionId} status changed to {NewStatus}, automatically removed from all approval queues (BR-217)",
                versionId, versionToReview.Status);

            // ========================================
            // GOOGLE DRIVE PERMISSIONS UPDATE
            // ========================================
            // COMMENTED OUT: Permission updates slow down approval process
            // Users are already invited to view folders when they are created
            // Permission updates invite whole department/company members which is slow
            /*
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
            */

            _logger.LogInformation("Manager {UserId} has {Action} document version {VersionId}", userId, logAction, versionId);

            // ========================================
            // CREATE FOLDER-AWARE RESPONSE
            // ========================================
            var response = new ApprovalReviewResponse
            {
                DocumentVersionId = versionId,
                DocumentTitle = versionToReview.Title,
                Decision = request.IsApproved ? "Approved" : "Rejected",
                Comments = request.Comments,
                PreviousStatus = "Pending",
                NewStatus = versionToReview.Status.ToString(),
                ReviewedBy = userId,
                ReviewedAt = DateTime.UtcNow,
                ApprovalLogId = approvalLog.Id
            };

            // ✅ FOLDER-AWARE: Add source folder information
            if (versionToReview.Folder != null)
            {
                response.SourceFolder = MapToFolderSummary(versionToReview.Folder);
            }

            // ✅ FOLDER-AWARE: Add target folder information if moved (from any source)
            if (request.IsApproved && !string.IsNullOrEmpty(versionToReview.FolderId))
            {
                var targetFolder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(predicate: f => f.Id == versionToReview.FolderId);
                if (targetFolder != null)
                {
                    response.TargetFolder = MapToFolderSummary(targetFolder);
                    _logger.LogInformation("Added target folder {FolderName} to approval response", targetFolder.Name);
                }
            }

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

            // ✅ FOLDER-AWARE: Return the response with folder information
            return response;
        }

        /// <summary>
        /// ✅ FOLDER-AWARE: Helper method to map Folder entity to FolderSummaryResponse
        /// </summary>
        private static FolderSummaryResponse MapToFolderSummary(Folder folder)
        {
            return new FolderSummaryResponse
            {
                Id = folder.Id,
                Name = folder.Name,
                Description = folder.Description,
                FullPath = folder.FullPath,
                Level = folder.Level,
                IsSystemFolder = folder.IsSystemFolder,
                IsPublic = folder.IsPublic,
                FolderType = folder.FolderType,
                SubFolderCount = folder.SubFolderCount,
                DocumentCount = folder.DocumentCount,
                DepartmentId = folder.DepartmentId,
                CreatedTime = folder.CreatedTime,
                CreatedBy = folder.CreatedBy
            };
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
            version.LastUpdatedBy = userId; // Set to actual user who submitted
            version.LastUpdatedTime = DateTime.UtcNow; // Update timestamp
            version.LastSubmitted = DateTime.UtcNow; // Track submission time for BR-214 (7-day timeout)

            //4. ✅ NEW FOLDER DESIGN: Keep document in drafts folder during pending status
            // Documents stay in drafts until approved, then move directly to functional folders
            var fileId = version.GoogleDriveFileId ?? version.FilePath;
            try
            {
                _logger.LogInformation("Document {FileId} submitted for approval - staying in drafts folder until approved", fileId);
                // No file movement needed - document stays in drafts during pending status

                //5. Save changes to the database
                await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(version);
                await _unitOfWork.CommitAsync();
                _logger.LogInformation("Successfully submitted document {VersionId} for approval", versionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit document {VersionId} for approval", versionId);

                // ✅ NEW FOLDER DESIGN: No rollback needed since file never moved from drafts
                // Document stays in drafts folder during pending status, so no rollback required
                _logger.LogInformation("No file rollback needed - document {FileId} remained in drafts folder", fileId);
                try
                {
                    // No file movement rollback needed
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback file {FileId} to Drafts folder after submission failure", fileId);
                }
                throw;
            }

            //7. COMMENTED OUT: Update Google Drive permissions (Draft -> Pending: owner + department managers)
            // Permission updates slow down submission process by inviting department managers
            // Users already have folder-level access when they are created
            /*
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
            */

            //8. Send notifications
            try
            {
                var currentUser = _httpContextAccessor.HttpContext?.User;
                if (currentUser != null)
                {
                    // 8a. Send confirmation notification to submitter
                    var submitterEmail = JwtTokenHelper.GetUserEmail(_httpContextAccessor);
                    var submitterName = JwtTokenHelper.GetUserFullName(_httpContextAccessor);

                    if (!string.IsNullOrEmpty(submitterEmail))
                    {
                        await _notificationService.SendDocumentSubmissionConfirmationAsync(
                            versionId,
                            version.Title,
                            version.VersionName,
                            submitterEmail,
                            submitterName ?? "Document Submitter",
                            currentUser);
                        _logger.LogInformation("Document submission confirmation sent to submitter for document {VersionId}", versionId);
                    }

                    // 8b. Send notification to department managers
                    await _notificationService.SendDocumentSubmissionNotificationAsync(
                        versionId,
                        version.Title,
                        version.VersionName,
                        currentUser,
                        version.DocumentFile.DepartmentId);
                    _logger.LogInformation("Document submission notification sent to managers for document {VersionId}", versionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send submission notifications for document {VersionId}", versionId);
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

        /// <summary>
        /// Gets the current user role from JWT token
        /// </summary>
        /// <returns>User role</returns>
        private string GetRoleFromJwt()
        {
            return JwtTokenHelper.GetUserRole(_httpContextAccessor);
        }

        /// <summary>
        /// Archive an approved document manually (Manager only)
        /// Changes status from Approved to Archived and removes from Kernel Memory
        /// </summary>
        public async Task ArchiveDocumentAsync(string versionId, ArchiveDocumentRequest request)
        {
            // Get current user ID and department ID from JWT token
            var userId = GetCurrentUserId();
            var managerDepartmentId = GetCurrentUserDepartmentId();
            var userRole = GetRoleFromJwt();

            // Validate manager role and department access
            if (userRole != Roles.Manager)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, "Only managers can archive documents");
            }

            if (string.IsNullOrEmpty(managerDepartmentId))
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, "Manager department not found in authentication token");
            }

            // Get the document version to archive
            var versionToArchive = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType)
                                  .Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                                  .Include(v => v.Folder)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);

            var documentFile = versionToArchive.DocumentFile;

            // Validate department access
            if (documentFile.DepartmentId != managerDepartmentId)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessApprovalQueue);
            }

            // Validate document status - only approved documents can be archived manually
            if (versionToArchive.Status != StatusEnum.Approved)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST,
                    $"Only approved documents can be archived manually. Current status: {versionToArchive.Status}");
            }

            // Check if this is the only approved version (prevent archiving if it would leave no active version)
            var otherApprovedVersions = await _unitOfWork.GetRepository<DocumentVersion>()
                .CountAsync(predicate: v => v.DocumentFileId == documentFile.Id && v.Status == StatusEnum.Approved && v.Id != versionId);

            if (otherApprovedVersions == 0)
            {
                _logger.LogWarning("Attempting to archive the only approved version of document {DocumentId}", documentFile.Id);
                // Allow archiving but log warning - business may want to archive outdated documents even if no replacement exists
            }

            try
            {
                // Remove from Kernel Memory first (before database changes)
                var versionKmId = versionToArchive.Id.ToString();
                try
                {
                    await _memory.DeleteDocumentAsync(versionKmId).WaitAsync(TimeSpan.FromSeconds(10));
                    _logger.LogInformation("Removed archived version {VersionId} from Kernel Memory", versionId);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Timeout removing version {VersionId} from Kernel Memory during archival", versionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove version {VersionId} from Kernel Memory during archival", versionId);
                }

                // Update document status to archived
                versionToArchive.Status = StatusEnum.Archived;
                versionToArchive.IsOfficial = false;
                versionToArchive.LastUpdatedBy = userId;
                versionToArchive.LastUpdatedTime = DateTime.UtcNow;

                // Update document file metadata
                documentFile.LastUpdatedBy = userId;
                documentFile.LastUpdatedTime = DateTime.UtcNow;

                // Create approval log for archival action
                var approvalLog = new ApprovalLog
                {
                    Action = ApprovalAction.Archived,
                    Comments = request.ArchiveReason,
                    CreatedBy = userId,
                    LastUpdatedBy = userId,
                    DocumentVersionId = versionToArchive.Id,
                };

                await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(versionToArchive);
                await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentFile);
                await _unitOfWork.GetRepository<ApprovalLog>().InsertAsync(approvalLog);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully archived document version {VersionId} by manager {UserId} with reason: {Reason}",
                    versionId, userId, request.ArchiveReason);

                // Send notifications if requested
                if (request.NotifyOwner || request.NotifyUsers)
                {
                    try
                    {
                        await _notificationService.SendDocumentArchivedNotificationAsync(
                            versionToArchive,
                            request.ArchiveReason,
                            userId,
                            request.NotifyOwner,
                            request.NotifyUsers);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send archive notifications for document {VersionId}", versionId);
                        // Don't fail the entire operation for notification errors
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while archiving document version {VersionId}", versionId);
                throw;
            }
        }

        /// <summary>
        /// Permanently delete an archived document (Manager only)
        /// Removes from database, storage, and Kernel Memory
        /// </summary>
        public async Task DeleteArchivedDocumentAsync(string versionId, DeleteArchivedDocumentRequest request)
        {
            // Get current user ID and department ID from JWT token
            var userId = GetCurrentUserId();
            var managerDepartmentId = GetCurrentUserDepartmentId();
            var userRole = GetRoleFromJwt();

            // Validate manager role and department access
            if (userRole != Roles.Manager)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, "Only managers can delete archived documents");
            }

            if (string.IsNullOrEmpty(managerDepartmentId))
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, "Manager department not found in authentication token");
            }

            // Validate confirmation
            if (!request.ConfirmPermanentDeletion)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, "Permanent deletion confirmation is required");
            }

            // Get the archived document version to delete
            var versionToDelete = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType)
                                  .Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                                  .Include(v => v.ApprovalLogs)
                                  .Include(v => v.Folder)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);

            var documentFile = versionToDelete.DocumentFile;

            // Validate department access
            if (documentFile.DepartmentId != managerDepartmentId)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessApprovalQueue);
            }

            // Validate document status - only archived documents can be deleted
            if (versionToDelete.Status != StatusEnum.Archived)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST,
                    $"Only archived documents can be deleted. Current status: {versionToDelete.Status}");
            }

            // Check for dependencies that might prevent deletion
            var hasReplacements = await _unitOfWork.GetRepository<DocumentFile>()
                .AnyAsync(predicate: df => df.ReplacementId == documentFile.Id);

            if (hasReplacements && !request.ForceDelete)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                    "Cannot delete document that has replacement documents. Use ForceDelete to override.");
            }

            // Check if this version is referenced in bookmarks
            var hasBookmarks = await _unitOfWork.GetRepository<Bookmark>()
                .AnyAsync(predicate: b => b.DocumentVersionId == versionId);

            if (hasBookmarks && !request.ForceDelete)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                    "Cannot delete document that is bookmarked by users. Use ForceDelete to override.");
            }

            try
            {
                // Remove from Kernel Memory first (if still exists)
                var versionKmId = versionToDelete.Id.ToString();
                try
                {
                    await _memory.DeleteDocumentAsync(versionKmId).WaitAsync(TimeSpan.FromSeconds(10));
                    _logger.LogInformation("Removed deleted version {VersionId} from Kernel Memory", versionId);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Timeout removing version {VersionId} from Kernel Memory during deletion", versionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove version {VersionId} from Kernel Memory during deletion (may not exist)", versionId);
                }

                // Delete from storage (Google Drive)
                var fileId = versionToDelete.GoogleDriveFileId ?? versionToDelete.FilePath;
                if (!string.IsNullOrEmpty(fileId))
                {
                    try
                    {
                        await _storageService.DeleteFileAsync(fileId);
                        _logger.LogInformation("Deleted file {FileId} from storage", fileId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete file {FileId} from storage (may not exist)", fileId);
                        if (!request.ForceDelete)
                        {
                            throw new ErrorException(StatusCodes.Status500InternalServerError, ErrorCode.INTERNAL_SERVER_ERROR,
                                "Failed to delete file from storage. Use ForceDelete to ignore storage errors.");
                        }
                    }
                }

                // Create deletion log before removing the version
                var deletionLog = new ApprovalLog
                {
                    Action = ApprovalAction.Deleted,
                    Comments = $"Permanent deletion: {request.DeletionReason}",
                    CreatedBy = userId,
                    LastUpdatedBy = userId,
                    DocumentVersionId = versionToDelete.Id,
                };
                await _unitOfWork.GetRepository<ApprovalLog>().InsertAsync(deletionLog);

                // Remove related entities first (due to foreign key constraints)
                if (versionToDelete.DocumentTags.Any())
                {
                    foreach (var docTag in versionToDelete.DocumentTags.ToList())
                    {
                        _unitOfWork.GetRepository<DocumentTag>().DeleteAsync(docTag);
                    }
                }

                if (versionToDelete.ApprovalLogs.Any())
                {
                    foreach (var log in versionToDelete.ApprovalLogs.Where(l => l.Id != deletionLog.Id).ToList())
                    {
                        _unitOfWork.GetRepository<ApprovalLog>().DeleteAsync(log);
                    }
                }

                // Remove approval claims if any
                var approvalClaims = await _unitOfWork.GetRepository<ApprovalClaim>()
                    .GetListAsync(predicate: ac => ac.DocumentVersionId == versionId);
                foreach (var claim in approvalClaims)
                {
                    _unitOfWork.GetRepository<ApprovalClaim>().DeleteAsync(claim);
                }

                // Remove bookmarks if force delete is enabled
                if (request.ForceDelete && hasBookmarks)
                {
                    var bookmarks = await _unitOfWork.GetRepository<Bookmark>()
                        .GetListAsync(predicate: b => b.DocumentVersionId == versionId);
                    foreach (var bookmark in bookmarks)
                    {
                        _unitOfWork.GetRepository<Bookmark>().DeleteAsync(bookmark);
                    }
                }

                // Check if this is the last version of the document
                var otherVersions = await _unitOfWork.GetRepository<DocumentVersion>()
                    .CountAsync(predicate: v => v.DocumentFileId == documentFile.Id && v.Id != versionId);

                if (otherVersions == 0)
                {
                    // This is the last version, delete the entire document file
                    _unitOfWork.GetRepository<DocumentFile>().DeleteAsync(documentFile);
                    _logger.LogInformation("Deleted entire document file {DocumentId} as it had no remaining versions", documentFile.Id);
                }
                else
                {
                    // Update document file metadata
                    documentFile.LastUpdatedBy = userId;
                    documentFile.LastUpdatedTime = DateTime.UtcNow;
                    await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentFile);
                }

                // Finally, delete the document version
                _unitOfWork.GetRepository<DocumentVersion>().DeleteAsync(versionToDelete);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully deleted archived document version {VersionId} by manager {UserId} with reason: {Reason}",
                    versionId, userId, request.DeletionReason);

                // Send notifications if requested
                if (request.NotifyOwner)
                {
                    try
                    {
                        await _notificationService.SendDocumentDeletedNotificationAsync(
                            versionToDelete,
                            request.DeletionReason,
                            userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send deletion notifications for document {VersionId}", versionId);
                        // Don't fail the entire operation for notification errors
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting archived document version {VersionId}", versionId);
                throw;
            }
        }

        /// <summary>
        /// Permanently delete an entire document with all its versions (Manager only)
        /// Removes all versions from database, storage, and Kernel Memory
        /// </summary>
        public async Task DeleteEntireDocumentAsync(string documentId, DeleteArchivedDocumentRequest request)
        {
            // Get current user ID and department ID from JWT token
            var userId = GetCurrentUserId();
            var managerDepartmentId = GetCurrentUserDepartmentId();
            var userRole = GetRoleFromJwt();

            // Validate manager role and department access
            if (userRole != Roles.Manager)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, "Only managers can delete entire documents");
            }

            if (string.IsNullOrEmpty(managerDepartmentId))
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, "Manager department not found in authentication token");
            }

            // Validate confirmation
            if (!request.ConfirmPermanentDeletion)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, "Permanent deletion confirmation is required");
            }

            // Get the document file with all its versions
            var documentToDelete = await _unitOfWork.GetRepository<DocumentFile>()
                .SingleOrDefaultAsync(
                    predicate: df => df.Id == documentId,
                    include: i => i.Include(df => df.DocumentVersions)
                                  .ThenInclude(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                                  .Include(df => df.DocumentVersions)
                                  .ThenInclude(v => v.ApprovalLogs)
                                  .Include(df => df.DocumentType)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Document not found");

            // Validate department access
            if (documentToDelete.DepartmentId != managerDepartmentId)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessApprovalQueue);
            }

            // Check if any version is still approved (prevent deletion of active documents unless forced)
            var hasApprovedVersions = documentToDelete.DocumentVersions.Any(v => v.Status == StatusEnum.Approved);
            if (hasApprovedVersions && !request.ForceDelete)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                    "Cannot delete document with approved versions. Archive them first or use ForceDelete to override.");
            }

            // Check if any version is still pending approval
            var hasPendingVersions = documentToDelete.DocumentVersions.Any(v => v.Status == StatusEnum.Pending);
            if (hasPendingVersions && !request.ForceDelete)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                    "Cannot delete document with pending versions. Reject them first or use ForceDelete to override.");
            }

            // Check for dependencies that might prevent deletion
            var hasReplacements = await _unitOfWork.GetRepository<DocumentFile>()
                .AnyAsync(predicate: df => df.ReplacementId == documentId);

            if (hasReplacements && !request.ForceDelete)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                    "Cannot delete document that has replacement documents. Use ForceDelete to override.");
            }

            // Check if any version is referenced in bookmarks
            var versionIds = documentToDelete.DocumentVersions.Select(v => v.Id).ToList();
            var hasBookmarks = await _unitOfWork.GetRepository<Bookmark>()
                .AnyAsync(predicate: b => versionIds.Contains(b.DocumentVersionId));

            if (hasBookmarks && !request.ForceDelete)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                    "Cannot delete document that is bookmarked by users. Use ForceDelete to override.");
            }

            try
            {
                _logger.LogInformation("Starting deletion of entire document {DocumentId} with {VersionCount} versions",
                    documentId, documentToDelete.DocumentVersions.Count);

                // Remove all versions from Kernel Memory first
                foreach (var version in documentToDelete.DocumentVersions)
                {
                    var versionKmId = version.Id.ToString();
                    try
                    {
                        await _memory.DeleteDocumentAsync(versionKmId).WaitAsync(TimeSpan.FromSeconds(10));
                        _logger.LogInformation("Removed version {VersionId} from Kernel Memory", version.Id);
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("Timeout removing version {VersionId} from Kernel Memory during document deletion", version.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to remove version {VersionId} from Kernel Memory during document deletion (may not exist)", version.Id);
                    }
                }

                // Delete all files from storage (Google Drive)
                foreach (var version in documentToDelete.DocumentVersions)
                {
                    var fileId = version.GoogleDriveFileId ?? version.FilePath;
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        try
                        {
                            await _storageService.DeleteFileAsync(fileId);
                            _logger.LogInformation("Deleted file {FileId} from storage for version {VersionId}", fileId, version.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete file {FileId} from storage for version {VersionId} (may not exist)", fileId, version.Id);
                            if (!request.ForceDelete)
                            {
                                throw new ErrorException(StatusCodes.Status500InternalServerError, ErrorCode.INTERNAL_SERVER_ERROR,
                                    $"Failed to delete file from storage for version {version.Id}. Use ForceDelete to ignore storage errors.");
                            }
                        }
                    }
                }

                // Create deletion log before removing the document
                var deletionLog = new ApprovalLog
                {
                    Action = ApprovalAction.Deleted,
                    Comments = $"Entire document deletion: {request.DeletionReason}",
                    CreatedBy = userId,
                    LastUpdatedBy = userId,
                    DocumentVersionId = documentToDelete.DocumentVersions.First().Id, // Associate with first version for audit
                };
                await _unitOfWork.GetRepository<ApprovalLog>().InsertAsync(deletionLog);

                // Remove all related entities for each version
                foreach (var version in documentToDelete.DocumentVersions.ToList())
                {
                    // Remove document tags
                    if (version.DocumentTags.Any())
                    {
                        foreach (var docTag in version.DocumentTags.ToList())
                        {
                            _unitOfWork.GetRepository<DocumentTag>().DeleteAsync(docTag);
                        }
                    }

                    // Remove approval logs (except the deletion log we just created)
                    if (version.ApprovalLogs.Any())
                    {
                        foreach (var log in version.ApprovalLogs.Where(l => l.Id != deletionLog.Id).ToList())
                        {
                            _unitOfWork.GetRepository<ApprovalLog>().DeleteAsync(log);
                        }
                    }

                    // Remove approval claims
                    var approvalClaims = await _unitOfWork.GetRepository<ApprovalClaim>()
                        .GetListAsync(predicate: ac => ac.DocumentVersionId == version.Id);
                    foreach (var claim in approvalClaims)
                    {
                        _unitOfWork.GetRepository<ApprovalClaim>().DeleteAsync(claim);
                    }
                }

                // Remove bookmarks if force delete is enabled
                if (request.ForceDelete && hasBookmarks)
                {
                    var bookmarks = await _unitOfWork.GetRepository<Bookmark>()
                        .GetListAsync(predicate: b => versionIds.Contains(b.DocumentVersionId));
                    foreach (var bookmark in bookmarks)
                    {
                        _unitOfWork.GetRepository<Bookmark>().DeleteAsync(bookmark);
                    }
                }

                // Update replacement references if this document was replacing others
                if (hasReplacements && request.ForceDelete)
                {
                    var replacementDocs = await _unitOfWork.GetRepository<DocumentFile>()
                        .GetListAsync(predicate: df => df.ReplacementId == documentId);
                    foreach (var replacementDoc in replacementDocs)
                    {
                        replacementDoc.ReplacementId = null;
                        await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(replacementDoc);
                    }
                }

                // Finally, delete all document versions and the document file
                foreach (var version in documentToDelete.DocumentVersions.ToList())
                {
                    _unitOfWork.GetRepository<DocumentVersion>().DeleteAsync(version);
                }
                _unitOfWork.GetRepository<DocumentFile>().DeleteAsync(documentToDelete);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully deleted entire document {DocumentId} with all {VersionCount} versions by manager {UserId} with reason: {Reason}",
                    documentId, documentToDelete.DocumentVersions.Count, userId, request.DeletionReason);

                // Send notifications if requested
                if (request.NotifyOwner)
                {
                    try
                    {
                        await _notificationService.SendDocumentDeletedNotificationAsync(
                            documentToDelete.DocumentVersions.OrderByDescending(v => v.CreatedTime).First(), // Use latest version for notification
                            $"Entire document deleted: {request.DeletionReason}",
                            userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send deletion notifications for document {DocumentId}", documentId);
                        // Don't fail the entire operation for notification errors
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting entire document {DocumentId}", documentId);
                throw;
            }
        }

        #region Helper Methods for Notifications

        /// <summary>
        /// Get user email by ID from Auth service via MassTransit
        /// </summary>
        private async Task<string?> GetUserEmailByIdAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Getting user email for user ID: {UserId}", userId);
                var email = await _permissionManager.GetUserEmailAsync(userId);
                return string.IsNullOrWhiteSpace(email) ? null : email;
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
                var name = await _nameLookupService.GetUserNameAsync(userId);
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user name for user ID: {UserId}", userId);
                return null;
            }
        }

        #endregion

        /// <summary>
        /// BR-214: Auto-reject documents that have been pending for more than 7 days
        /// This method should be called by a background service
        /// </summary>
        public async Task ProcessExpiredSubmissionsAsync()
        {
            try
            {
                var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                var expiredDocuments = await _unitOfWork.GetRepository<DocumentVersion>()
                    .GetListAsync(
                        predicate: v => v.Status == StatusEnum.Pending && v.LastSubmitted.HasValue && v.LastSubmitted.Value <= sevenDaysAgo,
                        include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType)
                    );

                foreach (var expiredDocument in expiredDocuments)
                {
                    _logger.LogInformation("Auto-rejecting expired document {VersionId} submitted on {SubmissionDate}",
                        expiredDocument.Id, expiredDocument.LastSubmitted);

                    // Update status to rejected
                    expiredDocument.Status = StatusEnum.Rejected;
                    expiredDocument.LastUpdatedBy = "system"; // System auto-rejection
                    expiredDocument.LastUpdatedTime = DateTime.UtcNow;

                    // Create approval log for auto-rejection
                    var approvalLog = new ApprovalLog
                    {
                        Action = ApprovalAction.Rejected,
                        Comments = "Automatically rejected due to 7-day timeout (BR-214)",
                        CreatedBy = "system",
                        LastUpdatedBy = "system",
                        DocumentVersionId = expiredDocument.Id,
                    };

                    await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(expiredDocument);
                    await _unitOfWork.GetRepository<ApprovalLog>().InsertAsync(approvalLog);

                    // Release any active claims
                    var activeClaim = await _unitOfWork.GetRepository<ApprovalClaim>()
                        .SingleOrDefaultAsync(predicate: ac => ac.DocumentVersionId == expiredDocument.Id && ac.IsActive);
                    if (activeClaim != null)
                    {
                        activeClaim.IsActive = false;
                        activeClaim.LastUpdatedBy = "system";
                        activeClaim.LastUpdatedTime = DateTime.UtcNow;
                        await _unitOfWork.GetRepository<ApprovalClaim>().UpdateAsync(activeClaim);
                    }

                    // Send timeout notification to document owner
                    try
                    {
                        var ownerEmail = await GetUserEmailByIdAsync(expiredDocument.DocumentFile.OwnerId);
                        var ownerName = await GetUserNameByIdAsync(expiredDocument.DocumentFile.OwnerId);

                        if (!string.IsNullOrEmpty(ownerEmail))
                        {
                            // Create a system user claims principal for system notifications
                            var systemClaims = new System.Security.Claims.ClaimsPrincipal(
                                new System.Security.Claims.ClaimsIdentity(new[]
                                {
                                    new System.Security.Claims.Claim("sub", "system"),
                                    new System.Security.Claims.Claim("name", "System"),
                                    new System.Security.Claims.Claim("email", "system@company.com")
                                }, "system"));

                            await _notificationService.SendDocumentRejectionNotificationAsync(
                                expiredDocument.Id,
                                expiredDocument.Title,
                                expiredDocument.VersionName,
                                ownerEmail,
                                ownerName ?? "Document Owner",
                                systemClaims,
                                "Your document submission has been automatically rejected due to 7-day timeout. Please review and resubmit if needed.");
                        }
                    }
                    catch (Exception notificationEx)
                    {
                        _logger.LogError(notificationEx, "Failed to send timeout notification for document {VersionId}", expiredDocument.Id);
                    }
                }

                if (expiredDocuments.Any())
                {
                    await _unitOfWork.CommitAsync();
                    _logger.LogInformation("Auto-rejected {Count} expired documents", expiredDocuments.Count());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired document submissions");
                throw;
            }
        }

        /// <summary>
        /// Auto-release inactive claims that haven't been updated for more than 30 minutes
        /// This method should be called by a background service
        /// </summary>
        public async Task ProcessInactiveClaimsAsync()
        {
            try
            {
                var thirtyMinutesAgo = DateTime.UtcNow.AddMinutes(-10);
                var inactiveClaims = await _unitOfWork.GetRepository<ApprovalClaim>()
                    .GetListAsync(
                        predicate: ac => ac.IsActive && ac.LastUpdatedTime.HasValue && ac.LastUpdatedTime.Value <= thirtyMinutesAgo,
                        include: i => i.Include(ac => ac.DocumentVersion).ThenInclude(dv => dv.DocumentFile)
                    );

                foreach (var inactiveClaim in inactiveClaims)
                {
                    _logger.LogInformation("Auto-releasing inactive claim for document {VersionId} claimed by {UserId} at {ClaimedAt}",
                        inactiveClaim.DocumentVersionId, inactiveClaim.ClaimedBy, inactiveClaim.ClaimedAt);

                    // Release the inactive claim
                    inactiveClaim.IsActive = false;
                    inactiveClaim.LastUpdatedBy = "system";
                    inactiveClaim.LastUpdatedTime = DateTime.UtcNow;

                    await _unitOfWork.GetRepository<ApprovalClaim>().UpdateAsync(inactiveClaim);

                    // Send notification to the manager who lost the claim
                    try
                    {
                        var managerEmail = await GetUserEmailByIdAsync(inactiveClaim.ClaimedBy);
                        var managerName = await GetUserNameByIdAsync(inactiveClaim.ClaimedBy);

                        if (!string.IsNullOrEmpty(managerEmail))
                        {
                            // Create a system user claims principal for system notifications
                            var systemClaims = new System.Security.Claims.ClaimsPrincipal(
                                new System.Security.Claims.ClaimsIdentity(new[]
                                {
                                    new System.Security.Claims.Claim("sub", "system"),
                                    new System.Security.Claims.Claim("name", "System"),
                                    new System.Security.Claims.Claim("email", "system@company.com")
                                }, "system"));

                            await _notificationService.SendDocumentRejectionNotificationAsync(
                                inactiveClaim.DocumentVersionId,
                                inactiveClaim.DocumentVersion.Title,
                                inactiveClaim.DocumentVersion.VersionName,
                                managerEmail,
                                managerName ?? "Manager",
                                systemClaims,
                                "Your claim on this document has been automatically released due to inactivity. The document is now available for other managers to review.");
                        }
                    }
                    catch (Exception notificationEx)
                    {
                        _logger.LogError(notificationEx, "Failed to send claim release notification for document {VersionId}", inactiveClaim.DocumentVersionId);
                    }
                }

                if (inactiveClaims.Any())
                {
                    await _unitOfWork.CommitAsync();
                    _logger.LogInformation("Auto-released {Count} inactive claims", inactiveClaims.Count());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inactive claims");
                throw;
            }
        }

        /// <summary>
        /// Maps DocumentVersion to enhanced PendingDocumentResponse with additional fields
        /// </summary>
        private PendingDocumentResponse MapToEnhancedPendingDocumentResponse(DocumentVersion version)
        {
            var response = _mapper.Map<PendingDocumentResponse>(version);

            // Add additional fields
            response.Description = version.DocumentFile.Description;
            response.Summary = version.Summary;
            response.FileSize = version.FileSize;
            response.FileType = version.FileType;
            response.Tags = version.DocumentTags?.Select(dt => dt.Tag.Name).ToList() ?? new List<string>();
            response.CreatedTime = version.DocumentFile.CreatedTime;
            response.LastUpdatedTime = version.LastUpdatedTime;
            response.OwnerId = version.DocumentFile.OwnerId;

            // Calculate derived fields
            response.DaysSinceSubmission = version.LastSubmitted.HasValue
                ? (DateTime.UtcNow - version.LastSubmitted.Value).Days
                : 0;

            response.IsApproachingExpiration = response.DaysSinceSubmission >= 5;
            response.Priority = CalculatePriority(version);

            // Replacement relationship fields
            response.ReplacementId = version.DocumentFile?.ReplacementId;
            response.ReplacementDocumentName = version.DocumentFile?.ReplacementDocument?.Title;
            response.IsReplaced = version.DocumentFile?.IsReplaced ?? false;

            return response;
        }

        /// <summary>
        /// Calculates priority level based on document characteristics
        /// </summary>
        private string CalculatePriority(DocumentVersion version)
        {
            var daysSinceSubmission = version.LastSubmitted.HasValue
                ? (DateTime.UtcNow - version.LastSubmitted.Value).Days
                : 0;

            // High priority: approaching expiration or urgent document types
            if (daysSinceSubmission >= 5)
                return "High";

            // Medium priority: 3+ days old
            if (daysSinceSubmission >= 3)
                return "Medium";

            return "Normal";
        }

        /// <summary>
        /// Calculates comprehensive approval queue statistics for the department
        /// </summary>
        private async Task<ApprovalQueueStatistics> CalculateApprovalQueueStatisticsAsync(string departmentId)
        {
            var repo = _unitOfWork.GetRepository<DocumentVersion>();
            var now = DateTime.UtcNow;
            var sevenDaysAgo = now.AddDays(-7);
            var thirtyDaysAgo = now.AddDays(-30);

            // Get all document versions for the department
            var allVersions = await repo.GetListAsync(
                predicate: v => v.DocumentFile.DepartmentId == departmentId,
                include: i => i.Include(v => v.DocumentFile).Include(v => v.ApprovalClaim!)
            );

            var statistics = new ApprovalQueueStatistics
            {
                TotalPending = allVersions.Count(v => v.Status == StatusEnum.Pending),
                TotalApproved = allVersions.Count(v => v.Status == StatusEnum.Approved),
                TotalRejected = allVersions.Count(v => v.Status == StatusEnum.Rejected),
                TotalArchived = allVersions.Count(v => v.Status == StatusEnum.Archived),
                TotalBeingReviewed = allVersions.Count(v => v.ApprovalClaim != null && v.ApprovalClaim.IsActive),
                RecentSubmissions = allVersions.Count(v => v.LastSubmitted >= sevenDaysAgo),
                ApproachingExpiration = allVersions.Count(v => v.Status == StatusEnum.Pending &&
                                                              v.LastSubmitted.HasValue &&
                                                              (now - v.LastSubmitted.Value).Days >= 5)
            };

            // Calculate average processing time for approved/rejected documents in last 30 days
            var processedDocuments = allVersions
                .Where(v => (v.Status == StatusEnum.Approved || v.Status == StatusEnum.Rejected) &&
                           v.LastSubmitted.HasValue &&
                           v.LastUpdatedTime.HasValue &&
                           v.LastUpdatedTime >= thirtyDaysAgo)
                .ToList();

            if (processedDocuments.Any())
            {
                var totalProcessingHours = processedDocuments
                    .Sum(v => (v.LastUpdatedTime!.Value - v.LastSubmitted!.Value).TotalHours);

                statistics.AverageProcessingTimeHours = totalProcessingHours / processedDocuments.Count;
            }

            return statistics;
        }

        /// <summary>
        /// Get the department root folder ID for approved documents
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <returns>Department root folder ID</returns>
        private async Task<string?> GetDepartmentRootFolderAsync(string? departmentId)
        {
            try
            {
                if (string.IsNullOrEmpty(departmentId))
                {
                    _logger.LogWarning("Department ID is null or empty, cannot get root folder");
                    return null;
                }

                // Get the department root folder (non-system folder with no parent)
                var departmentRootFolder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(
                        predicate: f => f.DepartmentId == departmentId &&
                                       f.ParentFolderId == null &&
                                       !f.IsSystemFolder &&
                                       !f.IsDeleted
                    );

                if (departmentRootFolder != null)
                {
                    _logger.LogInformation("Found department root folder {FolderId} for department {DepartmentId}",
                        departmentRootFolder.Id, departmentId);
                    return departmentRootFolder.Id;
                }

                _logger.LogWarning("No department root folder found for department {DepartmentId}", departmentId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department root folder for department {DepartmentId}", departmentId);
                return null;
            }
        }

        /// <summary>
        /// ✅ NEW: Manually archive an approved document
        /// BR-300: Managers can manually archive approved documents within their department
        /// </summary>
        public async Task<ArchiveDocumentResponse> ArchiveDocumentAsync(string versionId, ArchiveDocumentRequest request)
        {
            // Get current user ID and department ID from JWT token
            var userId = GetCurrentUserId();
            var managerDepartmentId = GetCurrentUserDepartmentId();

            // Validate input
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.ArchiveReasonRequired);

            if (request.Reason.Trim().Length < 10)
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.ArchiveReasonTooShort);

            // Get document version with all necessary includes
            var versionToArchive = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType!)
                                  .Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                                  .Include(v => v.Folder!)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);

            var documentFile = versionToArchive.DocumentFile;

            // Check permission - manager can only archive documents in their department
            if (documentFile.DepartmentId != managerDepartmentId)
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToArchiveDocument);

            // Check document status - can only archive approved documents
            if (versionToArchive.Status != StatusEnum.Approved)
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.CanOnlyArchiveApprovedDocuments);

            // Check if document is already archived
            if (versionToArchive.Status == StatusEnum.Archived)
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.DocumentAlreadyArchived);

            // Business Rule: Check if there are active replacement documents in progress
            // (Similar to how we check in DocumentService.DeleteApprovedDocumentAsync)
            if (!request.ForceArchive)
            {
                var hasActiveReplacements = await _unitOfWork.GetRepository<DocumentFile>()
                    .CountAsync(predicate: df => df.ReplacementId == documentFile.Id &&
                                               df.DocumentVersions.Any(v => v.Status == StatusEnum.Pending || v.Status == StatusEnum.Draft)) > 0;

                if (hasActiveReplacements)
                    throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.CannotArchiveDocumentWithActiveReplacements);
            }

            var warnings = new List<string>();
            bool removedFromKernelMemory = false;

            try
            {
                // ========================================
                // ARCHIVE DOCUMENT PROCESS
                // ========================================

                // 1. Remove from Kernel Memory index (similar to how replacement works)
                var versionKmId = $"doc_{versionToArchive.Id}";
                
                try
                {
                    await _memory.DeleteDocumentAsync(versionKmId).WaitAsync(TimeSpan.FromSeconds(10));
                    removedFromKernelMemory = true;
                    _logger.LogInformation("Removed archived document {VersionId} from Kernel Memory with ID {KmId}", versionId, versionKmId);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to remove document from Kernel Memory index: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to remove document {VersionId} from Kernel Memory during archival", versionId);
                }

                // 2. Update document status to archived
                versionToArchive.Status = StatusEnum.Archived;
                versionToArchive.LastUpdatedBy = userId;
                versionToArchive.LastUpdatedTime = DateTime.UtcNow;

                // 3. Update document file metadata
                documentFile.LastUpdatedBy = userId;
                documentFile.LastUpdatedTime = DateTime.UtcNow;

                // 4. Create an approval log entry for the archive action
                var archiveLog = new ApprovalLog
                {
                    DocumentVersionId = versionId,
                    Action = ApprovalAction.Archived,
                    Comments = $"Manually archived by manager. Reason: {request.Reason}" + 
                               (string.IsNullOrEmpty(request.Comments) ? "" : $". Additional comments: {request.Comments}"),
                    CreatedBy = userId,
                    CreatedTime = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<ApprovalLog>().InsertAsync(archiveLog);

                // 5. Save all changes
                await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(versionToArchive);
                await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentFile);
                await _unitOfWork.CommitAsync();

                // 6. Prepare response
                var response = new ArchiveDocumentResponse
                {
                    DocumentVersionId = versionId,
                    DocumentFileId = documentFile.Id,
                    Title = documentFile.Title,
                    VersionName = versionToArchive.VersionName,
                    Status = versionToArchive.Status.ToString(),
                    ArchivedAt = DateTime.UtcNow,
                    ArchivedBy = userId,
                    Reason = request.Reason,
                    Comments = request.Comments,
                    RemovedFromKernelMemory = removedFromKernelMemory,
                    Warnings = warnings,
                    Message = MessageConstant.DocumentArchivedSuccessfully
                };

                // 7. Add folder information if available
                if (versionToArchive.Folder != null)
                {
                    response.Folder = MapToFolderSummary(versionToArchive.Folder);
                }

                // 8. Enrich with user name
                response.ArchivedByName = await _nameLookupService.GetUserNameAsync(userId);

                _logger.LogInformation("Document version {VersionId} manually archived by manager {UserId} for reason: {Reason}", 
                    versionId, userId, request.Reason);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while archiving document {VersionId}", versionId);
                throw;
            }
        }

        /// <summary>
        /// ✅ NEW: Permanently delete an archived document
        /// BR-301: Managers can permanently delete archived documents within their department
        /// </summary>
        public async Task<DeleteArchivedDocumentResponse> DeleteArchivedDocumentAsync(string versionId, DeleteArchivedDocumentRequest request)
        {
            // Get current user ID and department ID from JWT token
            var userId = GetCurrentUserId();
            var managerDepartmentId = GetCurrentUserDepartmentId();

            // Validate input
            if (!request.ConfirmPermanentDeletion)
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.ConfirmDeleteArchivedDocument);

            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.DeleteArchivedReasonRequired);

            if (request.Reason.Trim().Length < 10)
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.DeleteArchivedReasonTooShort);

            // Get document version with all necessary includes
            var versionToDelete = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType!)
                                  .Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                                  .Include(v => v.Folder!)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);

            var documentFile = versionToDelete.DocumentFile;

            // Check permission - manager can only delete archived documents in their department
            if (documentFile.DepartmentId != managerDepartmentId)
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToDeleteArchivedDocument);

            // Check document status - can only delete archived documents
            if (versionToDelete.Status != StatusEnum.Archived)
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.CanOnlyDeleteArchivedDocuments);

            var warnings = new List<string>();
            bool fileDeletedFromStorage = false;
            bool removedFromKernelMemory = false;
            int databaseRecordsDeleted = 0;

            try
            {
                // ========================================
                // DELETE ARCHIVED DOCUMENT PROCESS
                // ========================================

                // Store information for response before deletion
                var responseData = new
                {
                    DocumentVersionId = versionId,
                    DocumentFileId = documentFile.Id,
                    Title = documentFile.Title,
                    VersionName = versionToDelete.VersionName,
                    FilePath = versionToDelete.FilePath
                };

                // 1. Remove from Kernel Memory index (if still exists)
                var versionKmId = $"doc_{versionToDelete.Id}";
                
                try
                {
                    await _memory.DeleteDocumentAsync(versionKmId).WaitAsync(TimeSpan.FromSeconds(10));
                    removedFromKernelMemory = true;
                    _logger.LogInformation("Removed deleted archived document {VersionId} from Kernel Memory with ID {KmId}", versionId, versionKmId);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to remove document from Kernel Memory index: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to remove document {VersionId} from Kernel Memory during deletion", versionId);
                }

                // 2. Delete physical file from storage
                if (!string.IsNullOrEmpty(versionToDelete.FilePath))
                {
                    try
                    {
                        await _storageService.DeleteFileAsync(versionToDelete.FilePath);
                        fileDeletedFromStorage = true;
                        _logger.LogInformation("Deleted physical file {FilePath} for document {VersionId}", versionToDelete.FilePath, versionId);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Failed to delete physical file from storage: {ex.Message}");
                        _logger.LogWarning(ex, "Failed to delete physical file {FilePath} for document {VersionId}", versionToDelete.FilePath, versionId);
                    }
                }

                // 3. Create a final audit log entry before deletion
                var deleteLog = new ApprovalLog
                {
                    DocumentVersionId = versionId,
                    Action = ApprovalAction.Deleted, // Assuming this exists, or we can use a custom string
                    Comments = $"Permanently deleted archived document by manager. Reason: {request.Reason}",
                    CreatedBy = userId,
                    CreatedTime = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<ApprovalLog>().InsertAsync(deleteLog);
                databaseRecordsDeleted++;

                // 4. Delete related records in correct order (avoid foreign key constraints)
                
                // Delete document tags
                var documentTags = await _unitOfWork.GetRepository<DocumentTag>()
                    .GetListAsync(predicate: dt => dt.DocumentVersionId == versionId);
                
                if (documentTags.Any())
                {
                    foreach (var tag in documentTags)
                    {
                        _unitOfWork.GetRepository<DocumentTag>().DeleteAsync(tag);
                        databaseRecordsDeleted++;
                    }
                }

                // Delete approval claims
                var approvalClaims = await _unitOfWork.GetRepository<ApprovalClaim>()
                    .GetListAsync(predicate: ac => ac.DocumentVersionId == versionId);
                
                if (approvalClaims.Any())
                {
                    foreach (var claim in approvalClaims)
                    {
                        _unitOfWork.GetRepository<ApprovalClaim>().DeleteAsync(claim);
                        databaseRecordsDeleted++;
                    }
                }

                // Delete approval logs (except the one we just created)
                var approvalLogs = await _unitOfWork.GetRepository<ApprovalLog>()
                    .GetListAsync(predicate: al => al.DocumentVersionId == versionId && al.Id != deleteLog.Id);
                
                if (approvalLogs.Any())
                {
                    foreach (var log in approvalLogs)
                    {
                        _unitOfWork.GetRepository<ApprovalLog>().DeleteAsync(log);
                        databaseRecordsDeleted++;
                    }
                }

                // Delete bookmarks (using DocumentId which maps to DocumentFileId)
                var bookmarks = await _unitOfWork.GetRepository<Bookmark>()
                    .GetListAsync(predicate: b => b.DocumentId == documentFile.Id);
                
                if (bookmarks.Any())
                {
                    foreach (var bookmark in bookmarks)
                    {
                        _unitOfWork.GetRepository<Bookmark>().DeleteAsync(bookmark);
                        databaseRecordsDeleted++;
                    }
                }

                // 5. Check if this is the last version of the document file
                var otherVersions = await _unitOfWork.GetRepository<DocumentVersion>()
                    .GetListAsync(predicate: v => v.DocumentFileId == documentFile.Id && v.Id != versionId);

                // 6. Delete the document version
                _unitOfWork.GetRepository<DocumentVersion>().DeleteAsync(versionToDelete);
                databaseRecordsDeleted++;

                // 7. If this was the last version, handle replacement relationships and delete the document file
                if (!otherVersions.Any())
                {
                    // Before deleting the DocumentFile, handle replacement relationships to avoid foreign key violations
                    // We need to handle BOTH directions of the replacement chain properly
                    
                    // 7a. Handle documents that reference this document as their replacement (forward references)
                    var documentsReplacedByThis = await _unitOfWork.GetRepository<DocumentFile>()
                        .GetListAsync(predicate: df => df.ReplacementId == documentFile.Id);
                    
                    // 7b. Handle documents that this document replaces (reverse references)
                    var documentsReplacingThis = await _unitOfWork.GetRepository<DocumentFile>()
                        .GetListAsync(predicate: df => df.ReplacedById == documentFile.Id);
                    
                    // 7c. Get the document that this document replaces (if any)
                    DocumentFile? documentBeingReplaced = null;
                    if (!string.IsNullOrEmpty(documentFile.ReplacementId))
                    {
                        documentBeingReplaced = await _unitOfWork.GetRepository<DocumentFile>()
                            .SingleOrDefaultAsync(predicate: df => df.Id == documentFile.ReplacementId);
                    }
                    
                    // 7d. Get the document that replaces this document (if any)
                    DocumentFile? documentReplacingThis = null;
                    if (!string.IsNullOrEmpty(documentFile.ReplacedById))
                    {
                        documentReplacingThis = await _unitOfWork.GetRepository<DocumentFile>()
                            .SingleOrDefaultAsync(predicate: df => df.Id == documentFile.ReplacedById);
                    }
                    
                    // 7e. Clear forward references (documents that point to this as replacement)
                    if (documentsReplacedByThis.Any())
                    {
                        foreach (var docReplacedByThis in documentsReplacedByThis)
                        {
                            // If there's a chain, try to maintain it by linking to the next document
                            if (documentReplacingThis != null)
                            {
                                docReplacedByThis.ReplacementId = documentReplacingThis.Id;
                                _logger.LogInformation("Updated replacement chain: Document {OldReplacedDoc} now points to {NewReplacementDoc} instead of deleted document {DeletedDoc}", 
                                    docReplacedByThis.Id, documentReplacingThis.Id, documentFile.Id);
                            }
                            else
                            {
                                docReplacedByThis.ReplacementId = null;
                                _logger.LogInformation("Cleared replacement reference from document {DocumentId} to deleted document {DeletedDocumentId}", 
                                    docReplacedByThis.Id, documentFile.Id);
                            }
                            await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(docReplacedByThis);
                        }
                        databaseRecordsDeleted += documentsReplacedByThis.Count;
                    }
                    
                    // 7f. Clear reverse references (documents that this document replaces)
                    if (documentsReplacingThis.Any())
                    {
                        foreach (var docReplacingThis in documentsReplacingThis)
                        {
                            // If there's a chain, try to maintain it by linking to the previous document
                            if (documentBeingReplaced != null)
                            {
                                docReplacingThis.ReplacedById = documentBeingReplaced.Id;
                                _logger.LogInformation("Updated reverse replacement chain: Document {NewReplacingDoc} now replaces {OldReplacedDoc} instead of deleted document {DeletedDoc}", 
                                    docReplacingThis.Id, documentBeingReplaced.Id, documentFile.Id);
                            }
                            else
                            {
                                docReplacingThis.ReplacedById = null;
                                _logger.LogInformation("Cleared reverse replacement reference from document {DocumentId} to deleted document {DeletedDocumentId}", 
                                    docReplacingThis.Id, documentFile.Id);
                            }
                            await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(docReplacingThis);
                        }
                        databaseRecordsDeleted += documentsReplacingThis.Count;
                    }
                    
                    // 7g. Clear this document's own replacement references
                    bool documentUpdated = false;
                    if (!string.IsNullOrEmpty(documentFile.ReplacementId))
                    {
                        documentFile.ReplacementId = null;
                        documentUpdated = true;
                        _logger.LogInformation("Cleared forward replacement reference from deleted document {DocumentId}", documentFile.Id);
                    }
                    
                    if (!string.IsNullOrEmpty(documentFile.ReplacedById))
                    {
                        documentFile.ReplacedById = null;
                        documentUpdated = true;
                        _logger.LogInformation("Cleared reverse replacement reference from deleted document {DocumentId}", documentFile.Id);
                    }
                    
                    if (documentUpdated)
                    {
                        await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentFile);
                    }
                    
                    // 7h. Update replacement chains in the documents we're linking together
                    if (documentBeingReplaced != null && documentReplacingThis != null)
                    {
                        // Link the chain: documentBeingReplaced -> documentReplacingThis
                        documentBeingReplaced.ReplacedById = documentReplacingThis.Id;
                        documentReplacingThis.ReplacementId = documentBeingReplaced.Id;
                        
                        await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentBeingReplaced);
                        await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentReplacingThis);
                        
                        _logger.LogInformation("Linked replacement chain after deletion: {OldDoc} -> {NewDoc} (bypassing deleted {DeletedDoc})", 
                            documentBeingReplaced.Id, documentReplacingThis.Id, documentFile.Id);
                        databaseRecordsDeleted += 2;
                    }
                    
                    // 7i. Now it's safe to delete the document file
                    _unitOfWork.GetRepository<DocumentFile>().DeleteAsync(documentFile);
                    databaseRecordsDeleted++;
                    _logger.LogInformation("Deleted document file {DocumentFileId} as it had no remaining versions", documentFile.Id);
                }

                // 8. Save all changes
                await _unitOfWork.CommitAsync();

                // 9. Prepare response
                var response = new DeleteArchivedDocumentResponse
                {
                    DocumentVersionId = responseData.DocumentVersionId,
                    DocumentFileId = responseData.DocumentFileId,
                    Title = responseData.Title,
                    VersionName = responseData.VersionName,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = userId,
                    Reason = request.Reason,
                    FileDeletedFromStorage = fileDeletedFromStorage,
                    RemovedFromKernelMemory = removedFromKernelMemory,
                    DatabaseRecordsDeleted = databaseRecordsDeleted,
                    Warnings = warnings,
                    Message = MessageConstant.ArchivedDocumentDeletedSuccessfully
                };

                // 10. Enrich with user name
                response.DeletedByName = await _nameLookupService.GetUserNameAsync(userId);

                _logger.LogInformation("Archived document version {VersionId} permanently deleted by manager {UserId} for reason: {Reason}. {RecordCount} database records deleted.", 
                    versionId, userId, request.Reason, databaseRecordsDeleted);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting archived document {VersionId}", versionId);
                throw;
            }
        }

        // NOTE: PopulateReverseReplacementsForPendingDocumentsAsync method removed - reverse relationships now populated directly from database via ReplacedById field
    }
}
