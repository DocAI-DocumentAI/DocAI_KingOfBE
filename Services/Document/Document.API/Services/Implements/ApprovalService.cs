using AutoMapper;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Domain.Enums;
using Document.Domain.Models;
using Document.Infrastructure.Paginate;
using Document.Infrastructure.Repository.Interfaces;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.Office2010.Word;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using Shared.Exceptions;

namespace Document.API.Services.Implements
{
    public class ApprovalService : IApprovalService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<ApprovalService> _logger;
        private readonly IAzureStorageService _storageService;
        private readonly IKernelMemory _memory;
        public ApprovalService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<ApprovalService> logger, IAzureStorageService storageService, IKernelMemory kernelMemory) 
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _storageService = storageService;
            _memory = kernelMemory;
        }
        public async Task<IPaginate<PendingDocumentResponse>> GetApprovalQueueAsync(string departmentId, int pageNumber, int pageSize)
        {
            // Get all approval from department
            // 1. Build the IQueryable without executing it.
            var pendingDocuments = await _unitOfWork.GetRepository<DocumentVersion>()
                .GetPagingListAsync(
                selector: v => _mapper.Map<PendingDocumentResponse>(v),
                filter: null,
                predicate: v => v.Status == StatusEnum.Pending && v.DocumentFile.DepartmentId == departmentId,
                orderBy: null,
                page: pageNumber,
                size: pageSize
                );
            return pendingDocuments;
        }

        public async Task ClaimDocumentForReviewAsync(string versionId, string userId)
        {
            var versionToClaim = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.Id == versionId,
                    include: i => i.Include(v => v.DocumentFile)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, MessageConstant.DocumentVersionNotFound);

            if (versionToClaim.Status != StatusEnum.Pending)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, string.Format(MessageConstant.NotPendingApproval, versionToClaim.Status));
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

        public async Task ReleaseClaimAsync(string versionId, string userId)
        {
            var existingClaim = await _unitOfWork.GetRepository<ApprovalClaim>()
                .SingleOrDefaultAsync(predicate: ac => ac.DocumentVersionId == versionId && ac.IsActive);

            if (existingClaim == null)
            {
                throw new ErrorException(StatusCodes.Status404NotFound, MessageConstant.ClaimNotFound);
            }

            if (existingClaim.ClaimedBy != userId)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, MessageConstant.UnauthorizedToReleaseClaim);
            }

            existingClaim.IsActive = false;
            existingClaim.LastUpdatedBy = userId;
            existingClaim.LastUpdatedTime = DateTime.UtcNow;

            _unitOfWork.GetRepository<ApprovalClaim>().UpdateAsync(existingClaim);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Document version {VersionId} claim released by user {UserId}", versionId, userId);
        }

        public async Task ReviewDocument(string versionId, ReviewDocumentRequest request, string userId)
        {
            var versionToReview = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(
                predicate: v => v.Id == versionId,
                include: i => i.Include(v => v.DocumentFile)
            ) ?? throw new ErrorException(StatusCodes.Status404NotFound, MessageConstant.DocumentVersionNotFound);
            var documentFile = versionToReview.DocumentFile;

            //// --- Permission and State Validation ---
            //if (documentFile.DepartmentId != managerDepartmentId)
            //    throw new ErrorException(StatusCodes.Status403Forbidden, "You do not have permission to review documents for this department.");

            if (versionToReview.Status != StatusEnum.Pending)
                throw new ErrorException(StatusCodes.Status400BadRequest, string.Format(MessageConstant.NotPendingApproval, versionToReview.Status));

            ApprovalAction logAction;

            if (request.IsApproved)
            {
                var previousApprovedVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(predicate: v => v.DocumentFileId == documentFile.Id && v.Status == StatusEnum.Approved);

                try
                {
                    if (previousApprovedVersion != null)
                    {
                        await _storageService.MoveFileAsync(previousApprovedVersion.FileName, StorageFolderConstant.Approved, StorageFolderConstant.Archived);
                        previousApprovedVersion.FilePath = $"{StorageFolderConstant.Archived}/{previousApprovedVersion.FileName}";
                    }

                    await _storageService.MoveFileAsync(versionToReview.FileName, StorageFolderConstant.Pending, StorageFolderConstant.Approved);
                    versionToReview.FilePath = $"{StorageFolderConstant.Approved}/{versionToReview.FileName}";

                    var fileExists = false;
                    var retryCount = 0;
                    while (!fileExists && retryCount < 5)
                    {
                        fileExists = await _storageService.FileExistsAsync(versionToReview.FilePath);
                        if (!fileExists)
                        {
                            await Task.Delay(500);
                            retryCount++;
                        }
                    }

                    if (!fileExists)
                    {
                        throw new ErrorException(StatusCodes.Status500InternalServerError, MessageConstant.FileNotAvailableInApprovedFolder);
                    }

                    if (previousApprovedVersion != null)
                    {
                        var previousVersionKmId = previousApprovedVersion.Id.ToString();
                        var oldTags = new TagCollection
                        {
                            { "status", "archived" },
                            { "departmentId", documentFile.DepartmentId },
                            { "documentId", documentFile.Id.ToString() },
                            { "versionName", previousApprovedVersion.VersionName },
                            { "approvalDate", previousApprovedVersion.CreatedTime.ToString("yyyy-MM-dd") }
                        };
                        using (var fileStream = await _storageService.DownloadFileAsync(previousApprovedVersion.FilePath))
                        {
                            await _memory.ImportDocumentAsync(fileStream, previousApprovedVersion.FileName, documentId: previousVersionKmId, tags: oldTags);
                        }

                        previousApprovedVersion.Status = StatusEnum.Archived;
                        previousApprovedVersion.IsOfficial = false;
                        await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(previousApprovedVersion);
                        _logger.LogInformation("Archived previous version {VersionId} and updated its AI tags.", previousApprovedVersion.Id);
                    }

                    versionToReview.Status = StatusEnum.Approved;
                    versionToReview.IsOfficial = true;
                    logAction = ApprovalAction.Approve;

                    var tags = new TagCollection
                    {
                        { "status", "approved" },
                        { "departmentId", documentFile.DepartmentId },
                        { "documentId", documentFile.Id.ToString() },
                        { "versionName", versionToReview.VersionName },
                        { "approvalDate", DateTime.UtcNow.ToString("yyyy-MM-dd") }
                    };

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

                    if (previousApprovedVersion != null)
                    {
                        await _storageService.MoveFileAsync(previousApprovedVersion.FileName, StorageFolderConstant.Archived, StorageFolderConstant.Approved);
                    }
                    await _storageService.MoveFileAsync(versionToReview.FileName, StorageFolderConstant.Approved, StorageFolderConstant.Pending);

                    throw;
                }
            }
            else
            {
                // --- REJECTION LOGIC---
                if (string.IsNullOrWhiteSpace(request.Comments) || request.Comments.Length < 10)
                    throw new ErrorException(StatusCodes.Status400BadRequest, MessageConstant.CommentsRequiredForRejection);
                versionToReview.Status = StatusEnum.Rejected;
                logAction = ApprovalAction.Reject;
            }

            // --- Finalize and Log ---
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
                _unitOfWork.GetRepository<ApprovalClaim>().UpdateAsync(activeClaim);
            }

            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Manager {UserId} has {Action} document version {VersionId}", userId, logAction, versionId);

            // TODO: Send a notification to the document owner.
        }

        public async Task SubmitForApprovalAsync(string versionId, string userId)
        {
            //1. Get the document
            var version = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                predicate: v => v.Id == versionId,
                include: i => i.Include(v =>v.DocumentFile)
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

            //4. Move the document file to the "Pending" folder in Azure Storage
            await _storageService.MoveFileAsync(version.FileName, StorageFolderConstant.Drafts, StorageFolderConstant.Pending);
            version.FilePath = $"{StorageFolderConstant.Pending}/{version.FileName}";

            //5. Change the file path to point to the new location

            //6. Save changes to the database
            _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(version);
            await _unitOfWork.CommitAsync();
        }
    }
}
