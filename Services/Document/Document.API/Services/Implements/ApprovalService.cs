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
            var pendingDocuments = _unitOfWork.GetRepository<DocumentVersion>()
                .GetPagingListAsync(
                selector: v => new PendingDocumentResponse
                {
                    VersionId = v.Id,
                    VersionName = v.VersionName,
                    Title = v.DocumentFile.Title,
                    CreatedBy = v.CreatedBy,
                    CreatedAt = v.CreatedTime,
                    Status = v.Status.ToString(), // Convert Enum to string
                    DepartmentId = v.DocumentFile.DepartmentId,
                },
                filter: null,
                predicate: v => v.Status == StatusEnum.Pending && v.DocumentFile.DepartmentId == departmentId,
                orderBy: null,
                page: pageNumber,
                size: pageSize);

            return (IPaginate<PendingDocumentResponse>)pendingDocuments;
        }

        public async Task ReviewDocument(string versionId, ReviewDocumentRequest request, string userId)
        {
            var versionToReview = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(
                predicate: v => v.Id == versionId
            ) ?? throw new ErrorException(StatusCodes.Status404NotFound, "The specified document version was not found.");
            var documentFile = versionToReview.DocumentFile;

            //// --- Permission and State Validation ---
            //if (documentFile.DepartmentId != managerDepartmentId)
            //    throw new ErrorException(StatusCodes.Status403Forbidden, "You do not have permission to review documents for this department.");

            if (versionToReview.Status != StatusEnum.Pending)
                throw new ErrorException(StatusCodes.Status400BadRequest, $"This document version is not awaiting approval. Its current status is '{versionToReview.Status}'.");

            ApprovalAction logAction;

            if (request.IsApproved)
            {
                // --- APPROVAL LOGIC (REFACTORED) ---

                // 1. Archive the previously approved version, if one exists.
                var previousApprovedVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(predicate: v => v.DocumentFileId == documentFile.Id && v.Status == StatusEnum.Approved);

                if (previousApprovedVersion != null)
                {
                    // Move to subfolder archive
                    await _storageService.MoveFileAsync(previousApprovedVersion.FileName, StorageFolderConstant.Approved, StorageFolderConstant.Archived);
                    previousApprovedVersion.FilePath = $"{StorageFolderConstant.Archived}/{previousApprovedVersion.FileName}";

                    // --- MODIFICATION: Update tags in Kernel Memory instead of deleting ---
                    var previousVersionKmId = previousApprovedVersion.Id.ToString();
                    var oldTags = new TagCollection
                        {
                        { "status", "archived" },
                        { "departmentId", documentFile.DepartmentId },
                        { "documentId", documentFile.Id.ToString() },
                        { "versionName", previousApprovedVersion.VersionName },
                        { "approvalDate", previousApprovedVersion.CreatedTime.ToString("yyyy-MM-dd") }
                    };
                    await _memory.ImportDocumentAsync(previousApprovedVersion.FilePath, documentId: previousVersionKmId,
                        tags: oldTags);

                    previousApprovedVersion.Status = StatusEnum.Archived;
                    previousApprovedVersion.IsOfficial = false; // Set IsOfficial to false for the previously approved version
                    _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(previousApprovedVersion);
                    _logger.LogInformation("Archived previous version {VersionId} and updated its AI tags.", previousApprovedVersion.Id);
                }

                // 2. Move the current version's file to the "Approved" folder.
                await _storageService.MoveFileAsync(versionToReview.FileName, StorageFolderConstant.Pending, StorageFolderConstant.Approved);
                versionToReview.FilePath = $"{StorageFolderConstant.Approved}/{versionToReview.FileName}";
                versionToReview.Status = StatusEnum.Approved;
                versionToReview.IsOfficial = true; // Set IsOfficial to true for the newly approved version
                logAction = ApprovalAction.Approve;

                // --- MODIFICATION: Add structured tags during import ---
                // 3. Create a collection of tags to apply to the new approved version.
                var tags = new TagCollection
            {
                { "status", "approved" },
                { "departmentId", documentFile.DepartmentId },
                { "documentId", documentFile.Id.ToString() },
                { "versionName", versionToReview.VersionName },
                { "approvalDate", DateTime.UtcNow.ToString("yyyy-MM-dd") }
            };

                // 4. Index the newly approved document in Kernel Memory with the new tags.
                var versionKmId = versionToReview.Id.ToString();
                await _memory.ImportDocumentAsync(versionToReview.FilePath, documentId: versionKmId, tags: tags);
                _logger.LogInformation("Indexed approved version {VersionId} in Kernel Memory with structured tags.", versionId);

            }
            else
            {
                // --- REJECTION LOGIC---
                if (string.IsNullOrWhiteSpace(request.Comments))
                    throw new ErrorException(StatusCodes.Status400BadRequest, "Comments are required to reject a document.");
                versionToReview.Status = StatusEnum.Rejected;
                logAction = ApprovalAction.Reject;
            }

            // --- Finalize and Log ---
            documentFile.LastUpdatedBy = userId;
            documentFile.LastUpdatedTime = DateTime.UtcNow;
            _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(versionToReview);

            var approvalLog = new ApprovalLog
            {
                Action = logAction,
                Comments = request.Comments,
                CreatedBy = userId,
                DocumentVersionId = versionToReview.Id,
            };
            await _unitOfWork.GetRepository<ApprovalLog>().InsertAsync(approvalLog);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Manager {UserId} has {Action} document version {VersionId}", userId, logAction, versionId);

            // TODO: Send a notification to the document owner.
        }

        public async Task SubmitForApprovalAsync(string versionId, string userId)
        {
            //1. Get the document
            var version = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(predicate: v => v.Id == versionId) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Document version not found");
            //2. Check owner ID
            if (version.DocumentFile.OwnerId != userId)
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, "You are not authorized to submit this document for approval");
            }
            //3. Check if the version status 
            if (version.Status != StatusEnum.Draft)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, $"Document version cannot be submitted for approval. Current status: {version.Status}");
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
