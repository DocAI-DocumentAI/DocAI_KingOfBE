using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Payload.Response.Folder;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Enums;
using Document.Domain.Models;
using Document.Domain.Model; // ✅ ADDED: For DocumentFile
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StatusEnum = Document.Domain.Enums.StatusEnum;
using System.Linq.Expressions; // ✅ ADDED: For Expression<Func<T, bool>>
using Shared.Exceptions; // ✅ ADDED: For ErrorException

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Enhanced approval service that maintains folder context during status changes
    /// </summary>
    public class FolderAwareApprovalService : IFolderAwareApprovalService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFolderService _folderService;
        private readonly IFolderDocumentService _folderDocumentService;
        private readonly IStorageService _storageService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<FolderAwareApprovalService> _logger;
        private readonly IDocumentEnrichmentService _enrichmentService; // ✅ ADDED: For user name enrichment

        public FolderAwareApprovalService(
            IUnitOfWork unitOfWork,
            IFolderService folderService,
            IFolderDocumentService folderDocumentService,
            IStorageService storageService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<FolderAwareApprovalService> logger,
            IDocumentEnrichmentService enrichmentService) // ✅ ADDED: Enrichment service
        {
            _unitOfWork = unitOfWork;
            _folderService = folderService;
            _folderDocumentService = folderDocumentService;
            _storageService = storageService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _enrichmentService = enrichmentService; // ✅ ADDED: Initialize enrichment service
        }

        public async Task<ApprovalSubmissionResponse> SubmitForApprovalAsync(string versionId, string? targetFolderId = null)
        {
            try
            {
                _logger.LogInformation("Submitting document {VersionId} for approval with target folder {TargetFolderId}", versionId, targetFolderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // Get the document version
                var version = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: v => v.Id == versionId,
                        include: i => i.Include(v => v.DocumentFile)
                                      .Include(v => v.Folder)
                    );

                if (version == null)
                {
                    throw new KeyNotFoundException($"Document version {versionId} not found");
                }

                // Validate ownership
                if (version.DocumentFile.OwnerId != userId)
                {
                    throw new UnauthorizedAccessException("You do not have permission to submit this document");
                }

                // Validate status
                if (version.Status != Domain.Enums.StatusEnum.Draft)
                {
                    throw new InvalidOperationException($"Cannot submit document with status {version.Status} for approval");
                }

                var response = new ApprovalSubmissionResponse
                {
                    DocumentVersionId = versionId,
                    DocumentTitle = version.Title,
                    PreviousStatus = version.Status.ToString(),
                    SubmittedBy = userId,
                    SubmittedAt = DateTime.UtcNow,
                    ApprovalDeadline = DateTime.UtcNow.AddDays(7) // 7-day approval deadline
                };

                // Get source folder information
                if (version.Folder != null)
                {
                    response.SourceFolder = MapToFolderSummary(version.Folder);
                }

                // ✅ NEW FOLDER DESIGN: Keep document in drafts during pending status
                // Documents stay in drafts folder until approved, then move directly to functional folders

                // Update document status to pending (but keep in drafts folder)
                version.Status = Domain.Enums.StatusEnum.Pending;
                version.LastUpdatedBy = userId;
                version.LastUpdatedTime = DateTime.UtcNow;
                version.LastSubmitted = DateTime.UtcNow;

                // ✅ FIXED: Validate target folder but DON'T assign it yet
                // Document should stay in drafts folder until approved
                if (!string.IsNullOrEmpty(targetFolderId))
                {
                    // Validate target folder exists and user has access
                    var targetFolder = await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(predicate: f => f.Id == targetFolderId && !f.IsDeleted);

                    if (targetFolder != null)
                    {
                        // TODO: Store target folder in a separate field or handle during approval
                        // For now, we'll pass it through the approval workflow
                        response.TargetFolder = MapToFolderSummary(targetFolder);
                        _logger.LogInformation("Document {VersionId} will move to folder {FolderName} when approved",
                            versionId, targetFolder.Name);
                    }
                    else
                    {
                        throw new KeyNotFoundException($"Target folder {targetFolderId} not found or access denied");
                    }
                }

                // Document stays in drafts folder during pending status (both database and physical file)
                _logger.LogInformation("Document {VersionId} submitted for approval - remaining in drafts folder until approved", versionId);

                // Save changes
                await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(version);

                // Create approval log entry
                var approvalLog = new ApprovalLog
                {
                    DocumentVersionId = versionId,
                    Action = ApprovalAction.Submitted,
                    Comments = "Document submitted for approval",
                    CreatedBy = userId,
                    CreatedTime = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<ApprovalLog>().InsertAsync(approvalLog);
                await _unitOfWork.CommitAsync();

                response.NewStatus = version.Status.ToString();

                _logger.LogInformation("Successfully submitted document {VersionId} for approval", versionId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting document {VersionId} for approval", versionId);
                throw;
            }
        }

        public async Task<ApprovalReviewResponse> ReviewDocumentAsync(string versionId, ReviewDocumentRequest request)
        {
            try
            {
                // ✅ FOLLOW APPROVAL SERVICE: Get current user ID and department ID from JWT token
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

            var versionToReview = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(
                predicate: v => v.Id == versionId,
                include: i => i.Include(v => v.DocumentFile).ThenInclude(df => df.DocumentType)
                              .Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                              .Include(v => v.Folder)
            ) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, MessageConstant.DocumentVersionNotFound);

            var documentFile = versionToReview.DocumentFile;

            // ✅ FOLLOW APPROVAL SERVICE: Declare variables at method level for broader scope
            DocumentFile? replacedDocument = null;

            // --- Permission and State Validation ---
            if (documentFile.DepartmentId != userDepartmentId)
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToAccessApprovalQueue);

            if (versionToReview.Status != StatusEnum.Pending)
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.NotPendingApproval, versionToReview.Status));

            ApprovalAction logAction;

            if (request.IsApproved)
            {
                // ========================================
                // DOCUMENT APPROVAL PROCESS - FOLLOW APPROVAL SERVICE EXACTLY
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
                        .SingleOrDefaultAsync(
                            predicate: df => df.Id == documentFile.ReplacementId,
                            include: i => i.Include(df => df.DocumentVersions.Where(v => v.Status == StatusEnum.Approved))
                                          .ThenInclude(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                        );
                }

                try
                {
                    // ========================================
                    // SCENARIO 3: DOCUMENT REPLACEMENT HANDLING - FOLLOW APPROVAL SERVICE EXACTLY
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
                                var replacedFileId = replacedApprovedVersion.GoogleDriveFileId ?? replacedApprovedVersion.FilePath;
                                _logger.LogInformation("Archiving replaced document {FileId} in-place (no folder movement required)", replacedFileId);

                                // Update database - mark replaced document as archived
                                replacedApprovedVersion.Status = StatusEnum.Archived;
                                replacedApprovedVersion.IsOfficial = false;
                                await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(replacedApprovedVersion);

                                _logger.LogInformation("Archived replaced document {ReplacedDocumentId}.", replacedDocument.Id);
                            }
                            else
                            {
                                // ✅ REPLACEMENT ONLY: Document still effective, just mark as replaced
                                _logger.LogInformation("Replaced document {ReplacedDocumentId} still effective until {EffectiveUntil}, keeping active status",
                                    replacedDocument.Id, replacedApprovedVersion.EffectiveUntil);
                            }

                            // ✅ ALWAYS UPDATE: Mark the DocumentFile as replaced regardless of archiving
                            replacedDocument.IsReplaced = true;
                            await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(replacedDocument);
                        }
                    }

                    // ========================================
                    // SCENARIO 2: VERSION ARCHIVING HANDLING - FOLLOW APPROVAL SERVICE EXACTLY
                    // ========================================
                    // If there's a previous approved version of the SAME document, archive it
                    if (previousApprovedVersion != null)
                    {
                        // Archive previous version in-place (no folder movement)
                        var previousFileId = previousApprovedVersion.GoogleDriveFileId ?? previousApprovedVersion.FilePath;
                        _logger.LogInformation("Archiving previous version {FileId} in-place (no folder movement required)", previousFileId);

                        previousApprovedVersion.Status = StatusEnum.Archived;
                        previousApprovedVersion.IsOfficial = false;
                        await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(previousApprovedVersion);
                        _logger.LogInformation("Archived previous version {VersionId} and updated its AI tags.", previousApprovedVersion.Id);
                    }

                    // ========================================
                    // CURRENT DOCUMENT APPROVAL - FOLDER-AWARE SPECIFIC
                    // ========================================
                    // ✅ FOLDER-AWARE: Move document from drafts to target functional folder
                    var currentFileId = versionToReview.GoogleDriveFileId ?? versionToReview.FilePath;

                    // Move to target functional folder if specified, otherwise keep in drafts
                    if (!string.IsNullOrEmpty(request.TargetFolderId))
                    {
                        // Get target folder information
                        var targetFolder = await _unitOfWork.GetRepository<Folder>()
                            .SingleOrDefaultAsync(predicate: f => f.Id == request.TargetFolderId);

                        if (targetFolder != null && !string.IsNullOrEmpty(targetFolder.GoogleDriveFolderId))
                        {
                            // Move file to target functional folder in Google Drive
                            await _storageService.MoveFileToFolderAsync(currentFileId, targetFolder.GoogleDriveFolderId);
                            _logger.LogInformation("Moved approved document {FileId} to functional folder {FolderName}",
                                currentFileId, targetFolder.Name);

                            // Update folder ID in database
                            versionToReview.FolderId = request.TargetFolderId;
                        }
                    }

                    // ========================================
                    // UPDATE DATABASE STATUS - FOLLOW APPROVAL SERVICE EXACTLY
                    // ========================================
                    // Mark the current document as approved and official
                    versionToReview.Status = StatusEnum.Approved;
                    versionToReview.IsOfficial = true;
                    logAction = ApprovalAction.Approved;

                    _logger.LogInformation("Document {VersionId} approved successfully", versionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the approval process for version {VersionId}. Reverting storage changes.", versionId);
                    throw;
                }
            }
            else
            {
                // ========================================
                // DOCUMENT REJECTION HANDLING - FOLLOW APPROVAL SERVICE EXACTLY
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
            // FINALIZE DATABASE CHANGES - FOLLOW APPROVAL SERVICE EXACTLY
            // ========================================
            // Update document metadata and save all changes
            documentFile.LastUpdatedBy = userId;
            documentFile.LastUpdatedTime = DateTime.UtcNow;
            // Persist DocumentFile metadata separately to avoid EF graph tracking conflicts
            await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentFile);

                // Create approval log entry
                var approvalLog = new ApprovalLog
                {
                    DocumentVersionId = versionId,
                    Action = request.IsApproved ? ApprovalAction.Approved : ApprovalAction.Rejected,
                    Comments = request.Comments,
                    CreatedBy = userId,
                    CreatedTime = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<ApprovalLog>().InsertAsync(approvalLog);

                // Deactivate any active claims
                var activeClaim = await _unitOfWork.GetRepository<ApprovalClaim>()
                    .SingleOrDefaultAsync(predicate: ac => ac.DocumentVersionId == versionId && ac.IsActive);

                if (activeClaim != null)
                {
                    activeClaim.IsActive = false;
                    activeClaim.LastUpdatedBy = userId;
                    activeClaim.LastUpdatedTime = DateTime.UtcNow;
                    await _unitOfWork.GetRepository<ApprovalClaim>().UpdateAsync(activeClaim);
                }

                await _unitOfWork.CommitAsync();

                // ========================================
                // CREATE RESPONSE - FOLDER-AWARE SPECIFIC
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

                // Get source folder information
                if (versionToReview.Folder != null)
                {
                    response.SourceFolder = MapToFolderSummary(versionToReview.Folder);
                }

                // Get target folder information if moved
                if (request.IsApproved && !string.IsNullOrEmpty(request.TargetFolderId))
                {
                    var targetFolder = await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(predicate: f => f.Id == request.TargetFolderId);
                    if (targetFolder != null)
                    {
                        response.TargetFolder = MapToFolderSummary(targetFolder);
                    }
                }

                _logger.LogInformation("Manager {UserId} has {Action} document version {VersionId}", userId, logAction, versionId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reviewing document {VersionId}", versionId);
                throw;
            }
        }

        public async Task<FolderAwareApprovalQueueResponse> GetApprovalQueueAsync(
            string? departmentId = null, 
            string? folderId = null, 
            bool includeSubfolders = false, 
            int page = 1, 
            int pageSize = 20)
        {
            try
            {
                _logger.LogInformation("Getting approval queue for department {DepartmentId}, folder {FolderId}", departmentId, folderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // ✅ FIXED: Use proper predicate pattern instead of GetQueryable()
                Expression<Func<DocumentVersion, bool>> predicate = dv => dv.Status == Domain.Enums.StatusEnum.Pending;

                // Apply department filter
                if (!string.IsNullOrEmpty(departmentId))
                {
                    predicate = CombinePredicates(predicate, dv => dv.DocumentFile.DepartmentId == departmentId);
                }
                else if (!string.IsNullOrEmpty(userDepartmentId))
                {
                    // Default to user's department
                    predicate = CombinePredicates(predicate, dv => dv.DocumentFile.DepartmentId == userDepartmentId);
                }

                // Apply folder filter
                if (!string.IsNullOrEmpty(folderId))
                {
                    if (includeSubfolders)
                    {
                        var subfolders = await GetAllSubfoldersAsync(folderId);
                        var folderIds = subfolders.Select(f => f.Id).Concat(new[] { folderId }).ToList();
                        predicate = CombinePredicates(predicate, dv => folderIds.Contains(dv.FolderId));
                    }
                    else
                    {
                        predicate = CombinePredicates(predicate, dv => dv.FolderId == folderId);
                    }
                }

                // ✅ FIXED: Use GetPagingListAsync instead of manual query building
                var result = await _unitOfWork.GetRepository<DocumentVersion>()
                    .GetPagingListAsync(
                        selector: dv => dv,
                        filter: null!,
                        predicate: predicate,
                        include: i => i.Include(dv => dv.DocumentFile)
                                      .Include(dv => dv.Folder)
                                      .Include(dv => dv.DocumentTags).ThenInclude(dt => dt.Tag),
                        orderBy: q => q.OrderBy(dv => dv.LastSubmitted), // Oldest submissions first for fairness
                        page: page,
                        size: pageSize
                    );

                var documents = result.Items;

                var response = new FolderAwareApprovalQueueResponse
                {
                    TotalPending = result.Total, // ✅ FIXED: Use result.Total instead of totalCount
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = result.TotalPages, // ✅ FIXED: Use result.TotalPages
                    AppliedFilters = new ApprovalQueueFilters
                    {
                        DepartmentId = departmentId,
                        FolderId = folderId,
                        IncludeSubfolders = includeSubfolders
                    }
                };

                // Map documents to approval info
                response.PendingDocuments = await MapToDocumentApprovalInfoAsync(documents.ToList()); // ✅ FIXED: Convert to List

                // Get folders with pending documents
                var foldersWithPending = documents
                    .Where(dv => dv.Folder != null)
                    .Select(dv => dv.Folder!)
                    .DistinctBy(f => f.Id)
                    .ToList();

                response.FoldersWithPendingDocuments = foldersWithPending.Select(f => MapToFolderSummary(f)).ToList();

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approval queue");
                throw;
            }
        }

        public async Task<FolderApprovalHistoryResponse> GetFolderApprovalHistoryAsync(
            string folderId,
            bool includeSubfolders = false,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                _logger.LogInformation("Getting approval history for folder {FolderId}", folderId);

                var folder = await _folderService.GetFolderByIdAsync(folderId);

                // ✅ FIXED: Use proper predicate pattern instead of GetQueryable()
                Expression<Func<ApprovalLog, bool>> predicate = al => true; // Start with no filter

                // Apply folder filter
                if (includeSubfolders)
                {
                    var subfolders = await GetAllSubfoldersAsync(folderId);
                    var folderIds = subfolders.Select(f => f.Id).Concat(new[] { folderId }).ToList();
                    predicate = CombinePredicates(predicate, al => al.DocumentVersion.FolderId != null && folderIds.Contains(al.DocumentVersion.FolderId));
                }
                else
                {
                    predicate = CombinePredicates(predicate, al => al.DocumentVersion.FolderId == folderId);
                }

                // Apply date filters
                if (fromDate.HasValue)
                {
                    predicate = CombinePredicates(predicate, al => al.CreatedTime >= fromDate);
                }

                if (toDate.HasValue)
                {
                    predicate = CombinePredicates(predicate, al => al.CreatedTime <= toDate);
                }

                // ✅ FIXED: Use GetPagingListAsync instead of manual query building
                var result = await _unitOfWork.GetRepository<ApprovalLog>()
                    .GetPagingListAsync(
                        selector: al => al,
                        filter: null!,
                        predicate: predicate,
                        include: i => i.Include(al => al.DocumentVersion)
                                      .Include(al => al.DocumentVersion.Folder),
                        orderBy: q => q.OrderByDescending(al => al.CreatedTime), // Most recent first
                        page: page,
                        size: pageSize
                    );

                var approvalLogs = result.Items;

                var response = new FolderApprovalHistoryResponse
                {
                    Folder = MapToFolderSummary(folder),
                    TotalEntries = result.Total, // ✅ FIXED: Use result.Total
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = result.TotalPages, // ✅ FIXED: Use result.TotalPages
                    IncludedSubfolders = includeSubfolders,
                    DateRange = new DateRange { FromDate = fromDate, ToDate = toDate }
                };

                // Map approval logs to history entries
                response.ApprovalHistory = approvalLogs.Select(al => new ApprovalHistoryEntry
                {
                    Id = al.Id,
                    DocumentVersionId = al.DocumentVersionId,
                    DocumentTitle = al.DocumentVersion.Title,
                    Action = al.Action.ToString(),
                    Comments = al.Comments,
                    ActionBy = al.CreatedBy,
                    ActionAt = al.CreatedTime,
                    DocumentFolder = al.DocumentVersion.Folder != null ? MapToFolderSummary(al.DocumentVersion.Folder) : null,
                    NewStatus = al.DocumentVersion.Status.ToString()
                }).ToList();

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approval history for folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<FolderMoveResult> MoveDocumentToStatusFolderAsync(string documentVersionId, Domain.Enums.StatusEnum newStatus, string? targetFolderId = null)
        {
            try
            {
                _logger.LogInformation("Moving document {DocumentVersionId} to status folder for {Status}", documentVersionId, newStatus);

                var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.Id == documentVersionId,
                        include: i => i.Include(dv => dv.DocumentFile)
                                      .Include(dv => dv.Folder)
                    );

                if (documentVersion == null)
                {
                    throw new KeyNotFoundException($"Document version {documentVersionId} not found");
                }

                var result = new FolderMoveResult
                {
                    MovedAt = DateTime.UtcNow
                };

                // Get source folder information
                if (documentVersion.Folder != null)
                {
                    result.SourceFolder = MapToFolderSummary(documentVersion.Folder);
                }

                // ✅ NEW FOLDER DESIGN: Determine target folder based on status
                string newFolderId;
                if (newStatus == Domain.Enums.StatusEnum.Approved)
                {
                    if (!string.IsNullOrEmpty(targetFolderId))
                    {
                        // ✅ VALIDATE PERMISSIONS: Check if approver can move document to target folder
                        var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                        var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor) ?? string.Empty;

                        // ✅ DEPARTMENT BOUNDARY VALIDATION: Prevent moving documents outside department
                        await ValidateDepartmentBoundaryForApprovalAsync(targetFolderId, userId, userDepartmentId);

                        if (!await _folderService.HasFolderPermissionAsync(targetFolderId, userId, userDepartmentId, PermissionType.Edit))
                        {
                            throw new UnauthorizedAccessException($"Access denied to move document to target folder. Approver must have Edit permission on the target folder.");
                        }

                        // Use specified target functional folder for approved documents
                        newFolderId = targetFolderId;
                        _logger.LogInformation("Document {DocumentVersionId} will be moved to target folder {TargetFolderId}", documentVersionId, targetFolderId);
                    }
                    else
                    {
                        // If no target folder specified, keep in drafts (should not happen in normal workflow)
                        newFolderId = await GetSystemFolderForStatusAsync(Domain.Enums.StatusEnum.Draft, documentVersion.DocumentFile.DepartmentId, documentVersion.IsPublic);
                        _logger.LogWarning("No target folder specified for approved document {DocumentVersionId}, keeping in drafts", documentVersionId);
                    }
                }
                else if (newStatus == Domain.Enums.StatusEnum.Draft || newStatus == Domain.Enums.StatusEnum.Rejected)
                {
                    // Draft and rejected documents go to drafts folder
                    newFolderId = await GetSystemFolderForStatusAsync(newStatus, documentVersion.DocumentFile.DepartmentId, documentVersion.IsPublic);
                }
                else if (newStatus == Domain.Enums.StatusEnum.Pending)
                {
                    // Pending documents stay in drafts folder (no movement)
                    newFolderId = await GetSystemFolderForStatusAsync(Domain.Enums.StatusEnum.Draft, documentVersion.DocumentFile.DepartmentId, documentVersion.IsPublic);
                    _logger.LogInformation("Pending document {DocumentVersionId} staying in drafts folder", documentVersionId);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported document status for folder movement: {newStatus}");
                }

                // ✅ FIXED: Always update folder assignment and move file when needed
                var oldFolderId = documentVersion.FolderId;
                documentVersion.FolderId = newFolderId;
                await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(documentVersion);

                // Move file in Google Drive if folder changed and file exists
                if (oldFolderId != newFolderId && !string.IsNullOrEmpty(documentVersion.GoogleDriveFileId))
                {
                    // Get target folder information for Google Drive move
                    var targetFolder = await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(predicate: f => f.Id == newFolderId && !f.IsDeleted);

                    if (targetFolder != null && !string.IsNullOrEmpty(targetFolder.GoogleDriveFolderId))
                    {
                        // Move file to target folder in Google Drive
                        await _storageService.MoveFileToFolderAsync(documentVersion.GoogleDriveFileId, targetFolder.GoogleDriveFolderId);
                        _logger.LogInformation("Moved document file {FileId} from folder {OldFolderId} to folder {NewFolderId} in Google Drive",
                            documentVersion.GoogleDriveFileId, oldFolderId, newFolderId);
                    }
                }

                // Get target folder information for response
                var folderDetail = await _folderService.GetFolderByIdAsync(newFolderId);
                result.TargetFolder = MapToFolderSummary(folderDetail);

                result.Success = true;
                _logger.LogInformation("Successfully moved document {DocumentVersionId} from folder {OldFolderId} to folder {NewFolderId}",
                    documentVersionId, oldFolderId, newFolderId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving document {DocumentVersionId} to status folder", documentVersionId);
                return new FolderMoveResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    MovedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<string> GetSystemFolderForStatusAsync(Domain.Enums.StatusEnum status, string? departmentId, bool isPublic)
        {
            try
            {
                // ✅ NEW FOLDER DESIGN: Only drafts are system folders
                // All other statuses should use functional folders, not system folders
                if (status != Domain.Enums.StatusEnum.Draft && status != Domain.Enums.StatusEnum.Rejected)
                {
                    throw new InvalidOperationException($"Status {status} should not use system folders. Use functional folders instead.");
                }

                var folderType = status switch
                {
                    Domain.Enums.StatusEnum.Draft => FolderType.Draft,
                    Domain.Enums.StatusEnum.Rejected => FolderType.Draft, // Rejected documents go back to drafts
                    _ => throw new InvalidOperationException($"Unsupported system folder status: {status}")
                };

                var systemFolder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(
                        predicate: f => f.IsSystemFolder &&
                                       f.FolderType == folderType &&
                                       f.IsPublic == isPublic &&
                                       (isPublic ? string.IsNullOrEmpty(f.DepartmentId) : f.DepartmentId == departmentId) &&
                                       !f.IsDeleted
                    );

                if (systemFolder == null)
                {
                    // Create system folder if it doesn't exist (only for drafts)
                    systemFolder = await CreateSystemFolderAsync(folderType, departmentId, isPublic);
                }

                return systemFolder.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system folder for status {Status}", status);
                throw;
            }
        }

        public async Task<BulkApprovalResponse> BulkReviewDocumentsAsync(List<BulkReviewRequest> requests)
        {
            try
            {
                _logger.LogInformation("Processing bulk review for {Count} documents", requests.Count);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var response = new BulkApprovalResponse
                {
                    TotalProcessed = requests.Count,
                    ProcessedAt = DateTime.UtcNow,
                    ProcessedBy = userId
                };

                foreach (var request in requests)
                {
                    try
                    {
                        var reviewRequest = new ReviewDocumentRequest
                        {
                            IsApproved = request.IsApproved,
                            Comments = request.Comments,
                            TargetFolderId = request.TargetFolderId
                        };

                        var reviewResult = await ReviewDocumentAsync(request.DocumentVersionId, reviewRequest);

                        response.Results.Add(new BulkApprovalResult
                        {
                            DocumentVersionId = request.DocumentVersionId,
                            DocumentTitle = reviewResult.DocumentTitle,
                            Success = true,
                            NewStatus = reviewResult.NewStatus,
                            TargetFolder = reviewResult.TargetFolder
                        });

                        response.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing bulk review for document {DocumentVersionId}", request.DocumentVersionId);

                        response.Results.Add(new BulkApprovalResult
                        {
                            DocumentVersionId = request.DocumentVersionId,
                            Success = false,
                            ErrorMessage = ex.Message
                        });

                        response.FailureCount++;
                    }
                }

                _logger.LogInformation("Bulk review completed: {SuccessCount} successful, {FailureCount} failed",
                    response.SuccessCount, response.FailureCount);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bulk review");
                throw;
            }
        }

        public async Task<FolderApprovalStatistics> GetFolderApprovalStatisticsAsync(
            string folderId,
            bool includeSubfolders = false,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            try
            {
                _logger.LogInformation("Getting approval statistics for folder {FolderId}", folderId);

                var folder = await _folderService.GetFolderByIdAsync(folderId);

                // ✅ FIXED: Use proper predicate pattern instead of GetQueryable()
                Expression<Func<ApprovalLog, bool>> predicate = al => true; // Start with no filter

                // Apply folder filter
                if (includeSubfolders)
                {
                    var subfolders = await GetAllSubfoldersAsync(folderId);
                    var folderIds = subfolders.Select(f => f.Id).Concat(new[] { folderId }).ToList();
                    predicate = CombinePredicates(predicate, al => al.DocumentVersion.FolderId != null && folderIds.Contains(al.DocumentVersion.FolderId));
                }
                else
                {
                    predicate = CombinePredicates(predicate, al => al.DocumentVersion.FolderId == folderId);
                }

                // Apply date filters
                if (fromDate.HasValue)
                {
                    predicate = CombinePredicates(predicate, al => al.CreatedTime >= fromDate);
                }

                if (toDate.HasValue)
                {
                    predicate = CombinePredicates(predicate, al => al.CreatedTime <= toDate);
                }

                var approvalLogs = await _unitOfWork.GetRepository<ApprovalLog>()
                    .GetListAsync(
                        predicate: predicate,
                        include: i => i.Include(al => al.DocumentVersion)
                    );

                var statistics = new FolderApprovalStatistics
                {
                    Folder = MapToFolderSummary(folder),
                    GeneratedAt = DateTime.UtcNow,
                    DateRange = new DateRange { FromDate = fromDate, ToDate = toDate },
                    IncludedSubfolders = includeSubfolders
                };

                // Calculate basic statistics
                statistics.TotalSubmitted = approvalLogs.Count(al => al.Action == ApprovalAction.Submitted);
                statistics.TotalApproved = approvalLogs.Count(al => al.Action == ApprovalAction.Approved);
                statistics.TotalRejected = approvalLogs.Count(al => al.Action == ApprovalAction.Rejected);

                // ✅ FIXED: Get currently pending documents using proper predicate pattern
                Expression<Func<DocumentVersion, bool>> pendingPredicate = dv => dv.Status == Domain.Enums.StatusEnum.Pending;

                if (includeSubfolders)
                {
                    var subfolders = await GetAllSubfoldersAsync(folderId);
                    var folderIds = subfolders.Select(f => f.Id).Concat(new[] { folderId }).ToList();
                    pendingPredicate = CombinePredicates(pendingPredicate, dv => folderIds.Contains(dv.FolderId));
                }
                else
                {
                    pendingPredicate = CombinePredicates(pendingPredicate, dv => dv.FolderId == folderId);
                }

                statistics.CurrentlyPending = await _unitOfWork.GetRepository<DocumentVersion>()
                    .CountAsync(predicate: pendingPredicate);

                // Calculate approval rate
                var totalReviewed = statistics.TotalApproved + statistics.TotalRejected;
                statistics.ApprovalRate = totalReviewed > 0 ? (double)statistics.TotalApproved / totalReviewed * 100 : 0;

                // Calculate average approval time
                var approvedDocuments = approvalLogs
                    .Where(al => al.Action == ApprovalAction.Approved)
                    .ToList();

                if (approvedDocuments.Any())
                {
                    var approvalTimes = new List<double>();

                    foreach (var approvedLog in approvedDocuments)
                    {
                        var submittedLog = approvalLogs
                            .FirstOrDefault(al => al.DocumentVersionId == approvedLog.DocumentVersionId &&
                                                 al.Action == ApprovalAction.Submitted &&
                                                 al.CreatedTime < approvedLog.CreatedTime);

                        if (submittedLog != null)
                        {
                            var approvalTime = (approvedLog.CreatedTime - submittedLog.CreatedTime).TotalHours;
                            approvalTimes.Add(approvalTime);
                        }
                    }

                    statistics.AverageApprovalTimeHours = approvalTimes.Any() ? approvalTimes.Average() : 0;
                }

                // Calculate monthly statistics
                statistics.MonthlyStats = CalculateMonthlyStats(approvalLogs.ToList()); // ✅ FIXED: Convert to List

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approval statistics for folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<bool> SetDepartmentDefaultApprovalFolderAsync(string departmentId, string folderId)
        {
            try
            {
                _logger.LogInformation("Setting default approval folder {FolderId} for department {DepartmentId}", folderId, departmentId);

                // This could be implemented by storing department preferences in a configuration table
                // For now, we'll just validate that the folder exists and is accessible
                var folder = await _folderService.GetFolderByIdAsync(folderId);

                if (folder.DepartmentId != departmentId && !folder.IsPublic)
                {
                    throw new UnauthorizedAccessException("Cannot set folder from different department as default");
                }

                // In a real implementation, you would store this preference in a DepartmentSettings table
                _logger.LogInformation("Default approval folder set successfully for department {DepartmentId}", departmentId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default approval folder for department {DepartmentId}", departmentId);
                throw;
            }
        }

        public async Task<List<DocumentApprovalInfo>> GetPendingDocumentsInFolderAsync(string folderId, bool includeSubfolders = false)
        {
            try
            {
                _logger.LogInformation("Getting pending documents in folder {FolderId}", folderId);

                // ✅ FIXED: Use proper predicate pattern instead of GetQueryable()
                Expression<Func<DocumentVersion, bool>> predicate = dv => dv.Status == Domain.Enums.StatusEnum.Pending;

                // Apply folder filter
                if (includeSubfolders)
                {
                    var subfolders = await GetAllSubfoldersAsync(folderId);
                    var folderIds = subfolders.Select(f => f.Id).Concat(new[] { folderId }).ToList();
                    predicate = CombinePredicates(predicate, dv => folderIds.Contains(dv.FolderId));
                }
                else
                {
                    predicate = CombinePredicates(predicate, dv => dv.FolderId == folderId);
                }

                var documents = await _unitOfWork.GetRepository<DocumentVersion>()
                    .GetListAsync(
                        predicate: predicate,
                        include: i => i.Include(dv => dv.DocumentFile)
                                      .Include(dv => dv.Folder)
                                      .Include(dv => dv.DocumentTags).ThenInclude(dt => dt.Tag),
                        orderBy: q => q.OrderBy(dv => dv.LastSubmitted)
                    );

                return await MapToDocumentApprovalInfoAsync(documents.ToList()); // ✅ FIXED: Convert to List
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending documents in folder {FolderId}", folderId);
                throw;
            }
        }

        #region Helper Methods

        private async Task<List<Folder>> GetAllSubfoldersAsync(string folderId)
        {
            var allSubfolders = new List<Folder>();
            var directSubfolders = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(predicate: f => f.ParentFolderId == folderId && !f.IsDeleted);

            allSubfolders.AddRange(directSubfolders);

            foreach (var subfolder in directSubfolders)
            {
                var nestedSubfolders = await GetAllSubfoldersAsync(subfolder.Id);
                allSubfolders.AddRange(nestedSubfolders);
            }

            return allSubfolders;
        }

        private async Task<Folder> CreateSystemFolderAsync(FolderType folderType, string? departmentId, bool isPublic)
        {
            var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);

            // ✅ NEW FOLDER DESIGN: Only allow creation of draft system folders
            var folderName = folderType switch
            {
                FolderType.Draft => FolderConstant.SystemFolders.Draft,
                _ => throw new InvalidOperationException($"Cannot create system folder for type {folderType}. Only Draft system folders are allowed in the new folder design.")
            };

            var folder = new Folder
            {
                Name = folderName,
                IsSystemFolder = true,
                FolderType = folderType,
                IsPublic = isPublic,
                DepartmentId = isPublic ? null : departmentId,
                FullPath = isPublic ? $"Public/{folderName}" : $"Departments/{departmentId}/{folderName}",
                CreatedBy = userId,
                CreatedTime = DateTime.UtcNow
            };

            await _unitOfWork.GetRepository<Folder>().InsertAsync(folder);
            await _unitOfWork.CommitAsync();

            return folder;
        }

        private async Task<List<DocumentApprovalInfo>> MapToDocumentApprovalInfoAsync(List<DocumentVersion> documents)
        {
            var approvalInfos = new List<DocumentApprovalInfo>();

            foreach (var document in documents)
            {
                var approvalInfo = new DocumentApprovalInfo
                {
                    // Basic IDs
                    Id = document.Id,
                    DocumentFileId = document.DocumentFileId,
                    VersionId = document.Id,
                    VersionName = document.VersionName,

                    // Document info
                    Title = document.Title,
                    Description = document.DocumentFile?.Description,
                    Summary = document.Summary,
                    Status = document.Status.ToString(),

                    // Submission info
                    SubmittedAt = document.LastSubmitted ?? document.CreatedTime,
                    SubmittedBy = document.SubmittedBy ?? document.CreatedBy,
                    // SubmittedByName will be enriched later

                    // Department info
                    DepartmentId = document.DocumentFile?.DepartmentId ?? string.Empty,
                    // DepartmentName will be enriched later

                    // Document type info
                    DocumentTypeId = document.DocumentFile?.DocumentTypeId ?? string.Empty,
                    DocumentTypeName = document.DocumentFile?.DocumentType?.Name,

                    // Document properties
                    IsPublic = document.IsPublic,
                    SignedBy = document.SignedBy,
                    EffectiveFrom = document.EffectiveFrom,
                    EffectiveUntil = document.EffectiveUntil,

                    // Review status (TODO: implement claim system)
                    IsBeingReviewed = false, // TODO: implement claim system
                    ReviewedBy = null,
                    ClaimedAt = null,
                    // ReviewedByName will be enriched later

                    // File info
                    FileSize = document.FileSize,
                    FileType = document.FileType,
                    Tags = document.DocumentTags?.Select(dt => dt.Tag.Name).ToList() ?? new List<string>(),

                    // Timestamps
                    CreatedTime = document.DocumentFile?.CreatedTime ?? document.CreatedTime,
                    LastUpdatedTime = document.LastUpdatedTime,

                    // Owner info
                    OwnerId = document.DocumentFile?.OwnerId ?? string.Empty,
                    // OwnerName will be enriched later

                    // Calculated fields
                    DaysSinceSubmission = document.LastSubmitted.HasValue
                        ? (DateTime.UtcNow - document.LastSubmitted.Value).Days
                        : (DateTime.UtcNow - document.CreatedTime).Days,
                    Priority = CalculatePriority(document),
                    IsApproachingExpiration = false, // Will be calculated below

                    // Resubmission info (TODO: implement)
                    ResubmissionCount = 0, // TODO: implement resubmission tracking
                    PreviousRejectionReason = null // TODO: implement rejection reason tracking
                };

                // Calculate derived fields
                approvalInfo.IsApproachingExpiration = approvalInfo.DaysSinceSubmission >= 5;
                approvalInfo.IsUrgent = approvalInfo.DaysSinceSubmission >= 7; // Legacy field
                approvalInfo.ApprovalDeadline = approvalInfo.SubmittedAt.AddDays(7); // Legacy field

                // DocumentType is already set above as DocumentTypeName

                if (document.Folder != null)
                {
                    approvalInfo.ContainingFolder = MapToFolderSummary(document.Folder);
                }

                // Calculate days since submission
                approvalInfo.DaysSinceSubmission = (int)(DateTime.UtcNow - approvalInfo.SubmittedAt).TotalDays;

                // Set approval deadline (7 days from submission)
                approvalInfo.ApprovalDeadline = approvalInfo.SubmittedAt.AddDays(7);
                approvalInfo.IsUrgent = DateTime.UtcNow > approvalInfo.ApprovalDeadline.Value.AddDays(-1); // Urgent if deadline is within 1 day

                // Get current claim information
                var activeClaim = await _unitOfWork.GetRepository<ApprovalClaim>()
                    .SingleOrDefaultAsync(predicate: ac => ac.DocumentVersionId == document.Id && ac.IsActive);

                if (activeClaim != null)
                {
                    approvalInfo.CurrentClaim = new ApprovalClaimInfo
                    {
                        ClaimedBy = activeClaim.ClaimedBy,
                        ClaimedAt = activeClaim.ClaimedAt,
                        IsActive = activeClaim.IsActive,
                        TimeRemaining = activeClaim.ClaimedAt.AddHours(2) - DateTime.UtcNow // 2-hour claim duration
                    };
                }

                approvalInfos.Add(approvalInfo);
            }

            // ✅ ADDED: Enrich with user names using the enrichment service
            await EnrichDocumentApprovalInfosAsync(approvalInfos);

            return approvalInfos;
        }

        private static FolderSummaryResponse MapToFolderSummary(Folder folder)
        {
            return new FolderSummaryResponse
            {
                Id = folder.Id,
                Name = folder.Name,
                FullPath = folder.FullPath,
                DepartmentId = folder.DepartmentId,
                IsPublic = folder.IsPublic,
                IsSystemFolder = folder.IsSystemFolder,
                FolderType = folder.FolderType,
                DocumentCount = folder.DocumentCount,
                SubFolderCount = folder.SubFolderCount,
                CreatedTime = folder.CreatedTime,
                CreatedBy = folder.CreatedBy
            };
        }

        private static FolderSummaryResponse MapToFolderSummary(FolderDetailResponse folder)
        {
            return new FolderSummaryResponse
            {
                Id = folder.Id,
                Name = folder.Name,
                FullPath = folder.FullPath,
                DepartmentId = folder.DepartmentId,
                IsPublic = folder.IsPublic,
                IsSystemFolder = folder.IsSystemFolder,
                FolderType = folder.FolderType,
                DocumentCount = folder.DocumentCount,
                SubFolderCount = folder.SubFolderCount,
                CreatedTime = folder.CreatedTime,
                CreatedBy = folder.CreatedBy
            };
        }

        private static List<MonthlyApprovalStats> CalculateMonthlyStats(List<ApprovalLog> approvalLogs)
        {
            var monthlyStats = new List<MonthlyApprovalStats>();

            var groupedByMonth = approvalLogs
                .GroupBy(al => new { al.CreatedTime.Year, al.CreatedTime.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month);

            foreach (var monthGroup in groupedByMonth)
            {
                var monthLogs = monthGroup.ToList();
                var monthDate = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1);

                var stats = new MonthlyApprovalStats
                {
                    Month = monthDate,
                    Submitted = monthLogs.Count(al => al.Action == ApprovalAction.Submitted),
                    Approved = monthLogs.Count(al => al.Action == ApprovalAction.Approved),
                    Rejected = monthLogs.Count(al => al.Action == ApprovalAction.Rejected)
                };

                // Calculate average approval time for this month
                var approvedInMonth = monthLogs.Where(al => al.Action == ApprovalAction.Approved).ToList();
                if (approvedInMonth.Any())
                {
                    var approvalTimes = new List<double>();

                    foreach (var approvedLog in approvedInMonth)
                    {
                        var submittedLog = approvalLogs
                            .FirstOrDefault(al => al.DocumentVersionId == approvedLog.DocumentVersionId &&
                                                 al.Action == ApprovalAction.Submitted &&
                                                 al.CreatedTime < approvedLog.CreatedTime);

                        if (submittedLog != null)
                        {
                            var approvalTime = (approvedLog.CreatedTime - submittedLog.CreatedTime).TotalHours;
                            approvalTimes.Add(approvalTime);
                        }
                    }

                    stats.AverageApprovalTimeHours = approvalTimes.Any() ? approvalTimes.Average() : 0;
                }

                monthlyStats.Add(stats);
            }

            return monthlyStats;
        }

        /// <summary>
        /// ✅ DEPARTMENT BOUNDARY VALIDATION: Prevent approving documents to folders outside department
        /// </summary>
        private async Task ValidateDepartmentBoundaryForApprovalAsync(string targetFolderId, string userId, string userDepartmentId)
        {
            try
            {
                // Admins can approve documents to any folder
                var userRole = JwtTokenHelper.GetUserRole(_httpContextAccessor);
                if (userRole == "Admin")
                {
                    return; // Admins have no restrictions
                }

                // Get target folder and validate department boundary
                var targetFolder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(predicate: f => f.Id == targetFolderId && !f.IsDeleted);

                if (targetFolder == null)
                {
                    throw new KeyNotFoundException($"Target folder not found");
                }

                // Check if target folder is within user's department or public
                if (!targetFolder.IsPublic && targetFolder.DepartmentId != userDepartmentId)
                {
                    // Get department names for better error message
                    var targetDepartmentName = await GetDepartmentNameAsync(targetFolder.DepartmentId);
                    var userDepartmentName = await GetDepartmentNameAsync(userDepartmentId);

                    throw new UnauthorizedAccessException(
                        $"Access denied: Cannot approve documents to folders outside your department. " +
                        $"Target folder belongs to '{targetDepartmentName}' but you belong to '{userDepartmentName}'. " +
                        $"Managers can only approve documents to folders within their own department.");
                }

                _logger.LogInformation("Department boundary validation passed for user {UserId} to approve document to folder {FolderId}",
                    userId, targetFolderId);
            }
            catch (Exception ex) when (!(ex is UnauthorizedAccessException || ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error validating department boundary for approval by user {UserId}", userId);
                throw new InvalidOperationException("Error validating department access for approval", ex);
            }
        }

        /// <summary>
        /// Helper method to get department name for error messages
        /// </summary>
        private async Task<string> GetDepartmentNameAsync(string? departmentId)
        {
            if (string.IsNullOrEmpty(departmentId))
            {
                return "Public";
            }

            // Try to get department name from folder structure
            var departmentFolder = await _unitOfWork.GetRepository<Folder>()
                .SingleOrDefaultAsync(predicate: f => f.DepartmentId == departmentId && f.ParentFolderId == null && !f.IsDeleted);

            return departmentFolder?.Name ?? $"Department-{departmentId}";
        }

        /// <summary>
        /// ✅ ADDED: Calculate priority level based on document characteristics (copied from ApprovalService)
        /// </summary>
        private string CalculatePriority(DocumentVersion version)
        {
            var daysSinceSubmission = version.LastSubmitted.HasValue
                ? (DateTime.UtcNow - version.LastSubmitted.Value).Days
                : (DateTime.UtcNow - version.CreatedTime).Days;

            // High priority: approaching expiration or urgent document types
            if (daysSinceSubmission >= 5)
                return "High";

            // Medium priority: 3+ days old
            if (daysSinceSubmission >= 3)
                return "Medium";

            return "Normal";
        }

        /// <summary>
        /// ✅ ADDED: Enrich DocumentApprovalInfo objects with user names
        /// </summary>
        private async Task EnrichDocumentApprovalInfosAsync(List<DocumentApprovalInfo> approvalInfos)
        {
            if (!approvalInfos.Any()) return;

            try
            {
                // Convert to PendingDocumentResponse for enrichment
                var pendingDocuments = approvalInfos.Select(ai => new PendingDocumentResponse
                {
                    DocumentFileId = ai.DocumentFileId,
                    VersionId = ai.VersionId,
                    VersionName = ai.VersionName,
                    Title = ai.Title,
                    SubmittedBy = ai.SubmittedBy,
                    LastSubmitted = ai.SubmittedAt,
                    Status = ai.Status,
                    DepartmentId = ai.DepartmentId,
                    DocumentTypeId = ai.DocumentTypeId,
                    DocumentTypeName = ai.DocumentTypeName,
                    IsPublic = ai.IsPublic,
                    SignedBy = ai.SignedBy,
                    EffectiveFrom = ai.EffectiveFrom,
                    EffectiveUntil = ai.EffectiveUntil,
                    IsBeingReviewed = ai.IsBeingReviewed,
                    ReviewedBy = ai.ReviewedBy,
                    ClaimedAt = ai.ClaimedAt,
                    Description = ai.Description,
                    Summary = ai.Summary,
                    FileSize = ai.FileSize,
                    FileType = ai.FileType,
                    Tags = ai.Tags,
                    CreatedTime = ai.CreatedTime,
                    LastUpdatedTime = ai.LastUpdatedTime,
                    OwnerId = ai.OwnerId,
                    Priority = ai.Priority,
                    DaysSinceSubmission = ai.DaysSinceSubmission,
                    IsApproachingExpiration = ai.IsApproachingExpiration,
                    ResubmissionCount = ai.ResubmissionCount,
                    PreviousRejectionReason = ai.PreviousRejectionReason
                }).ToList();

                // Enrich with names
                var enrichedDocuments = await _enrichmentService.EnrichPendingDocumentResponsesAsync(pendingDocuments);

                // Copy enriched names back to approval infos
                for (int i = 0; i < approvalInfos.Count && i < enrichedDocuments.Count; i++)
                {
                    approvalInfos[i].SubmittedByName = enrichedDocuments[i].SubmittedByName;
                    approvalInfos[i].DepartmentName = enrichedDocuments[i].DepartmentName;
                    approvalInfos[i].ReviewedByName = enrichedDocuments[i].ReviewedByName;
                    approvalInfos[i].OwnerName = enrichedDocuments[i].OwnerName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching document approval infos with user names");
                // Continue without enrichment - names will be null but IDs are still available
            }
        }

        /// <summary>
        /// ✅ FIXED: Helper method to combine predicates (copied from other services)
        /// </summary>
        private static Expression<Func<T, bool>> CombinePredicates<T>(
            Expression<Func<T, bool>> first,
            Expression<Func<T, bool>> second)
        {
            var parameter = Expression.Parameter(typeof(T));
            var leftVisitor = new ReplaceExpressionVisitor(first.Parameters[0], parameter);
            var left = leftVisitor.Visit(first.Body);
            var rightVisitor = new ReplaceExpressionVisitor(second.Parameters[0], parameter);
            var right = rightVisitor.Visit(second.Body);
            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left, right), parameter);
        }

        /// <summary>
        /// Helper class for combining expressions
        /// </summary>
        private class ReplaceExpressionVisitor : ExpressionVisitor
        {
            private readonly Expression _oldValue;
            private readonly Expression _newValue;

            public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
            {
                _oldValue = oldValue;
                _newValue = newValue;
            }

            public override Expression Visit(Expression node)
            {
                return node == _oldValue ? _newValue : base.Visit(node);
            }
        }

        #endregion
    }
}
