using Document.API.Constants;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Enums;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using Shared.Command;
using Shared.DTOs;

namespace Document.API.Consumers
{
    public class UpdateDocumentStatusConsumer : IConsumer<UpdateDocumentStatusCommand>
    {
        private readonly IDocumentExpirationService _expirationService;
        private readonly ILogger<UpdateDocumentStatusConsumer> _logger;
        private readonly IKernelMemory _memory; // ✅ THÊM: Kernel Memory
        private readonly IUnitOfWork _unitOfWork; // ✅ THÊM: UnitOfWork để lấy document details
        private readonly IStorageService _storageService; // ✅ THÊM: Storage service cho file access
        private readonly INameLookupService _nameLookupService; // ✅ THÊM: Name lookup service

        public UpdateDocumentStatusConsumer(
            IDocumentExpirationService expirationService,
            ILogger<UpdateDocumentStatusConsumer> logger,
            IKernelMemory memory,
            IUnitOfWork unitOfWork,
            IStorageService storageService,
            INameLookupService nameLookupService)
        {
            _expirationService = expirationService;
            _logger = logger;
            _memory = memory;
            _unitOfWork = unitOfWork;
            _storageService = storageService;
            _nameLookupService = nameLookupService;
        }

        public async Task Consume(ConsumeContext<UpdateDocumentStatusCommand> context)
        {
            var vietnamTime = context.Message.VietnamTime ?? VietnamTimeHelper.GetVietnamDateTime();

            try
            {
                _logger.LogInformation("Processing UpdateDocumentStatusCommand for document {DocumentId}/{Version} to {NewStatus} at Vietnam time {VietnamTime}",
                    context.Message.DocumentId, context.Message.Version, context.Message.NewStatus, vietnamTime);

                // ✅ Update document status qua existing service
                var result = await _expirationService.UpdateDocumentStatusAsync(
                    context.Message.DocumentId,
                    context.Message.Version,
                    context.Message.NewStatus);

                bool kernelMemoryUpdated = false;
                string? oldStatus = null;

                // ✅ Nếu update thành công và cần update kernel memory
                if (result && context.Message.UpdateKernelMemory)
                {
                    var (kmResult, oldStat) = await UpdateKernelMemoryForStatusChange(
                        context.Message.DocumentId,
                        context.Message.Version,
                        context.Message.NewStatus,
                        vietnamTime);

                    kernelMemoryUpdated = kmResult;
                    oldStatus = oldStat;
                }

                await context.RespondAsync(new UpdateDocumentStatusResponse
                {
                    Success = result,
                    DocumentId = context.Message.DocumentId,
                    Version = context.Message.Version,
                    OldStatus = oldStatus,
                    NewStatus = context.Message.NewStatus,
                    KernelMemoryUpdated = kernelMemoryUpdated,
                    RequestId = context.Message.RequestId
                });

                _logger.LogInformation("Successfully processed UpdateDocumentStatusCommand for {DocumentId}/{Version}, KM updated: {KMUpdated}",
                    context.Message.DocumentId, context.Message.Version, kernelMemoryUpdated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing UpdateDocumentStatusCommand for {DocumentId}/{Version}",
                    context.Message.DocumentId, context.Message.Version);

                await context.RespondAsync(new UpdateDocumentStatusResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    DocumentId = context.Message.DocumentId,
                    Version = context.Message.Version,
                    KernelMemoryUpdated = false,
                    RequestId = context.Message.RequestId
                });
            }
        }

        /// <summary>
        /// Update kernel memory metadata khi document status thay đổi
        /// Tham khảo logic từ ApprovalService.ReviewDocument
        /// </summary>
        private async Task<(bool success, string? oldStatus)> UpdateKernelMemoryForStatusChange(
            string documentId, string version, string newStatus, DateTime vietnamTime)
        {
            try
            {
                // ✅ Lấy document version với đầy đủ thông tin
                var document = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.DocumentFile.Id == documentId && dv.VersionName == version,
                        include: i => i.Include(dv => dv.DocumentFile)
                                      .ThenInclude(df => df.DocumentType)
                                      .Include(dv => dv.DocumentTags)
                                      .ThenInclude(dt => dt.Tag)
                                      .Include(dv => dv.Folder)
                    );

                if (document == null)
                {
                    _logger.LogWarning("Document not found for kernel memory update: {DocumentId}/{Version}", documentId, version);
                    return (false, null);
                }

                var oldStatus = document.Status.ToString();

                // ✅ Chỉ update kernel memory nếu chuyển từ Approved sang Archived
                if (document.Status == StatusEnum.Approved && newStatus == "Archived")
                {
                    await UpdateKernelMemoryToArchivedStatus(document, vietnamTime);
                    return (true, oldStatus);
                }

                _logger.LogDebug("No kernel memory update needed for status change from {OldStatus} to {NewStatus}",
                    oldStatus, newStatus);
                return (true, oldStatus); // Success nhưng không cần update KM
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating kernel memory for document {DocumentId}/{Version}", documentId, version);
                return (false, null);
            }
        }

        /// <summary>
        /// Update kernel memory metadata cho document chuyển sang archived status
        /// Tham khảo từ ApprovalService logic với đầy đủ metadata
        /// </summary>
        private async Task UpdateKernelMemoryToArchivedStatus(DocumentVersion document, DateTime vietnamTime)
        {
            try
            {
                var versionKmId = document.Id.ToString();
                _logger.LogInformation("Updating Kernel Memory metadata for archived document {VersionId} at Vietnam time {VietnamTime}",
                    document.Id, vietnamTime);

                // ✅ Tạo tags mới với status = archived (tham khảo ApprovalService với đầy đủ metadata)
                var tags = new TagCollection
                {
                    // ========================================
                    // CORE IDENTIFIERS - UPDATE STATUS TO ARCHIVED
                    // ========================================
                    { SemanticSearchConstant.MemoryTags.Status, "archived" }, // ✅ Thay đổi từ "approved" thành "archived"
                    { SemanticSearchConstant.MemoryTags.DocumentId, document.DocumentFile.Id.ToString() },
                    { SemanticSearchConstant.MemoryTags.DepartmentId, document.DocumentFile.DepartmentId },
                    { SemanticSearchConstant.MemoryTags.OwnerId, document.DocumentFile.OwnerId },
                    { SemanticSearchConstant.MemoryTags.Version, document.VersionName },
                    { SemanticSearchConstant.MemoryTags.IsOfficial, "false" }, // ✅ Không còn official khi archived
                    { SemanticSearchConstant.MemoryTags.IsPublic, document.IsPublic.ToString().ToLower() },
                    { SemanticSearchConstant.MemoryTags.CreatedBy, document.CreatedBy },
                    { SemanticSearchConstant.MemoryTags.SubmittedBy, document.SubmittedBy ?? document.CreatedBy },
                    { SemanticSearchConstant.MemoryTags.LastSubmitted, document.LastSubmitted?.ToString("o") },

                    //// ✅ THÊM metadata cho archived document
                    //{ "archivedDate", vietnamTime.ToString("yyyy-MM-dd") }, // Ngày archived theo Vietnam time
                    //{ "archivedBy", "system_expiration" }, // Lý do archived
                    //{ "archivedReason", "document_expired" }, // Chi tiết lý do

                    // ========================================
                    // DOCUMENT CORE METADATA - GIỮ NGUYÊN
                    // ========================================
                    { SemanticSearchConstant.MemoryTags.Title, document.DocumentFile.Title ?? "" },
                    { SemanticSearchConstant.MemoryTags.Description, document.DocumentFile.Description ?? "" },
                    { SemanticSearchConstant.MemoryTags.VersionTitle, document.Title ?? "" },
                    { SemanticSearchConstant.MemoryTags.Summary, document.Summary ?? "" },
                    { SemanticSearchConstant.MemoryTags.DocumentType, document.DocumentFile.DocumentTypeId ?? "" },
                    { SemanticSearchConstant.MemoryTags.SignedBy, document.SignedBy ?? "" },
                    { SemanticSearchConstant.MemoryTags.EffectiveFrom, document.EffectiveFrom?.ToString("yyyy-MM-dd") ?? "" },
                    { SemanticSearchConstant.MemoryTags.EffectiveUntil, document.EffectiveUntil?.ToString("yyyy-MM-dd") ?? "" },

                    // ========================================
                    // FILE SYSTEM METADATA
                    // ========================================
                    { SemanticSearchConstant.MemoryTags.FileName, document.FileName ?? "" },
                    { SemanticSearchConstant.MemoryTags.FileType, document.FileType ?? "" },
                    { SemanticSearchConstant.MemoryTags.FileSize, document.FileSize.ToString() },
                    { SemanticSearchConstant.MemoryTags.FileHash, document.FileHash ?? "" },
                    { SemanticSearchConstant.MemoryTags.GoogleDriveFileId, document.GoogleDriveFileId ?? document.FilePath ?? "" },
                    { SemanticSearchConstant.MemoryTags.StorageLocation, "GoogleDrive" }
                };

                // ========================================
                // CLASSIFICATION: TAGS VÀ DOCUMENT TYPE
                // ========================================
                if (document.DocumentTags != null && document.DocumentTags.Any())
                {
                    foreach (var docTag in document.DocumentTags)
                    {
                        if (!string.IsNullOrWhiteSpace(docTag.Tag?.Name))
                            tags.Add(SemanticSearchConstant.MemoryTags.Tags, docTag.Tag.Name);
                    }
                }

                // Document type friendly name/description nếu có
                if (document.DocumentFile.DocumentType != null)
                {
                    tags.Add(SemanticSearchConstant.MemoryTags.DocumentTypeName, document.DocumentFile.DocumentType.Name ?? "");
                    if (!string.IsNullOrWhiteSpace(document.DocumentFile.DocumentType.Description))
                        tags.Add(SemanticSearchConstant.MemoryTags.DocumentTypeDescription, document.DocumentFile.DocumentType.Description);
                }

                // ========================================
                // ORGANIZATIONAL METADATA
                // ========================================
                try
                {
                    var ownerName = await _nameLookupService.GetUserNameAsync(document.DocumentFile.OwnerId);
                    if (!string.IsNullOrWhiteSpace(ownerName))
                        tags.Add(SemanticSearchConstant.MemoryTags.OwnerName, ownerName);

                    var deptName = await _nameLookupService.GetDepartmentNameAsync(document.DocumentFile.DepartmentId);
                    if (!string.IsNullOrWhiteSpace(deptName))
                        tags.Add(SemanticSearchConstant.MemoryTags.DepartmentName, deptName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enrich organizational metadata for archived document {VersionId}", document.Id);
                }

                // ========================================
                // FOLDER METADATA NẾU CÓ
                // ========================================
                if (document.Folder != null)
                {
                    tags.Add(SemanticSearchConstant.MemoryTags.FolderId, document.Folder.Id);
                    tags.Add(SemanticSearchConstant.MemoryTags.FolderName, document.Folder.Name ?? "");
                    if (!string.IsNullOrWhiteSpace(document.Folder.FullPath))
                        tags.Add(SemanticSearchConstant.MemoryTags.FolderPath, document.Folder.FullPath);
                    if (!string.IsNullOrWhiteSpace(document.Folder.Description))
                        tags.Add(SemanticSearchConstant.MemoryTags.FolderDescription, document.Folder.Description);
                    tags.Add(SemanticSearchConstant.MemoryTags.FolderIsPublic, document.Folder.IsPublic.ToString().ToLower());
                }

                // ========================================
                // ACCESS CONTROL METADATA - CẬP NHẬT CHO ARCHIVED
                // ========================================
                tags.Add(SemanticSearchConstant.MemoryTags.Visibility, "archived"); // ✅ Visibility thành archived
                tags.Add(SemanticSearchConstant.MemoryTags.DepartmentRestriction, document.DocumentFile.DepartmentId);
                tags.Add(SemanticSearchConstant.MemoryTags.PermissionLevel, "archived-read-only"); // ✅ Permission level cho archived

                // ========================================
                // RELATIONSHIP METADATA - Nếu có thông tin replacement
                // ========================================
                if (!string.IsNullOrEmpty(document.DocumentFile.ReplacementId))
                {
                    tags.Add(SemanticSearchConstant.MemoryTags.ReplacementOfDocumentId, document.DocumentFile.ReplacementId);
                }

                // ========================================
                // CẬP NHẬT KERNEL MEMORY
                // ========================================
                using var kmCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                try
                {
                    // ✅ Theo pattern từ ApprovalService: Delete và re-import với metadata mới
                    // Đây là cách được sử dụng trong ApprovalService khi archive previous version
                    await _memory.DeleteDocumentAsync(versionKmId).WaitAsync(TimeSpan.FromSeconds(10));
                    _logger.LogInformation("Removed approved version {VersionId} from Kernel Memory for re-indexing as archived", document.Id);

                    // ✅ Re-index với metadata mới (archived status) và full content
                    if (!string.IsNullOrEmpty(document.GoogleDriveFileId) || !string.IsNullOrEmpty(document.FilePath))
                    {
                        var fileId = document.GoogleDriveFileId ?? document.FilePath;
                        using (var fileStream = await _storageService.DownloadFileAsync(fileId))
                        {
                            // Import lại document với full content và metadata archived
                            await _memory.ImportDocumentAsync(fileStream, document.FileName,
                                documentId: versionKmId, tags: tags, cancellationToken: kmCts.Token);
                        }

                        _logger.LogInformation("Successfully re-indexed document {VersionId} as archived in Kernel Memory at Vietnam time {VietnamTime}",
                            document.Id, vietnamTime);
                    }
                    else
                    {
                        _logger.LogWarning("No file path available for re-indexing document {VersionId}", document.Id);
                        throw new InvalidOperationException($"No file path available for document {document.Id}");
                    }
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Timeout updating Kernel Memory for archived document {VersionId}", document.Id);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update Kernel Memory for archived document {VersionId}", document.Id);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Kernel Memory metadata for archived document {VersionId} at Vietnam time {VietnamTime}",
                    document.Id, vietnamTime);
                throw; // Re-throw để consumer có thể handle và báo lỗi trong response
            }
        }
    }
}
