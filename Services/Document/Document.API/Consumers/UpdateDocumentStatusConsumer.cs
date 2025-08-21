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
        private readonly IKernelMemory _memory;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateDocumentStatusConsumer(
            IDocumentExpirationService expirationService,
            ILogger<UpdateDocumentStatusConsumer> logger,
            IKernelMemory memory,
            IUnitOfWork unitOfWork)
        {
            _expirationService = expirationService;
            _logger = logger;
            _memory = memory;
            _unitOfWork = unitOfWork;
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
        /// Remove embeddings from Kernel Memory when document expires
        /// Following ApprovalService pattern - complete removal instead of archiving
        /// </summary>
        private async Task<(bool success, string? oldStatus)> UpdateKernelMemoryForStatusChange(
            string documentId, string version, string newStatus, DateTime vietnamTime)
        {
            try
            {
                // ✅ Lấy document version để xác định status cũ
                var document = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.DocumentFile.Id == documentId && dv.VersionName == version,
                        include: i => i.Include(dv => dv.DocumentFile)
                    );

                if (document == null)
                {
                    _logger.LogWarning("Document not found for kernel memory update: {DocumentId}/{Version}", documentId, version);
                    return (false, null);
                }

                var oldStatus = document.Status.ToString();

                // ✅ Chỉ remove embeddings nếu chuyển từ Approved sang Archived
                if (document.Status == StatusEnum.Approved && newStatus == "Archived")
                {
                    await RemoveExpiredDocumentFromKernelMemory(document, vietnamTime);
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
        /// Remove expired document embeddings from Kernel Memory completely
        /// Following ApprovalService pattern - complete removal instead of archiving
        /// </summary>
        private async Task RemoveExpiredDocumentFromKernelMemory(DocumentVersion document, DateTime vietnamTime)
        {
            try
            {
                var versionKmId = document.Id.ToString();
                _logger.LogInformation("Removing expired document {VersionId} from Kernel Memory at Vietnam time {VietnamTime}",
                    document.Id, vietnamTime);
                // ✅ Complete removal like in ApprovalService - no re-indexing
                try
                {
                    await _memory.DeleteDocumentAsync(versionKmId).WaitAsync(TimeSpan.FromSeconds(10));
                    _logger.LogInformation("Removed expired document {VersionId} from Kernel Memory.", versionKmId);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Timeout removing expired document {VersionId} from Kernel Memory", versionKmId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove expired document {VersionId} from Kernel Memory", versionKmId);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing expired document {VersionId} from Kernel Memory at Vietnam time {VietnamTime}",
                    document.Id, vietnamTime);
                throw; // Re-throw để consumer có thể handle và báo lỗi trong response
            }
        }
    }
}
