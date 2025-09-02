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
using Shared.Utils;

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

        // 1. Thêm extensive logging vào UpdateDocumentStatusConsumer

        public async Task Consume(ConsumeContext<UpdateDocumentStatusCommand> context)
        {
            var vietnamTime = context.Message.VietnamTime ?? TimeZoneHelper.VietnamNow;

            try
            {
                _logger.LogInformation("Processing UpdateDocumentStatusCommand for {DocId}/{Version} to {NewStatus}",
                    context.Message.DocumentId, context.Message.Version, context.Message.NewStatus);

                // STEP 1: Get document with current status BEFORE any changes
                var documentInfo = await GetDocumentInfoForUpdate(context.Message.DocumentId, context.Message.Version);

                if (documentInfo == null)
                {
                    await RespondWithError(context, "Document not found");
                    return;
                }

                // STEP 2: Update document status
                var statusUpdateResult = await _expirationService.UpdateDocumentStatusAsync(
                    context.Message.DocumentId,
                    context.Message.Version,
                    context.Message.NewStatus);

                if (!statusUpdateResult)
                {
                    await RespondWithError(context, "Failed to update document status");
                    return;
                }

                // STEP 3: Handle Kernel Memory based on ORIGINAL status (before update)
                var kernelMemoryResult = await HandleKernelMemoryUpdate(
                    documentInfo,
                    context.Message.NewStatus,
                    context.Message.UpdateKernelMemory,
                    vietnamTime);

                // STEP 4: Send success response
                await context.RespondAsync(new UpdateDocumentStatusResponse
                {
                    Success = true,
                    DocumentId = context.Message.DocumentId,
                    Version = context.Message.Version,
                    OldStatus = documentInfo.CurrentStatus,
                    NewStatus = context.Message.NewStatus,
                    KernelMemoryUpdated = kernelMemoryResult.Updated,
                    ErrorMessage = kernelMemoryResult.Error,
                    RequestId = context.Message.RequestId
                });

                _logger.LogInformation("Successfully updated document {DocId}/{Version} from {OldStatus} to {NewStatus}, KM updated: {KMUpdated}",
                    context.Message.DocumentId, context.Message.Version,
                    documentInfo.CurrentStatus, context.Message.NewStatus, kernelMemoryResult.Updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing UpdateDocumentStatusCommand for {DocId}/{Version}",
                    context.Message.DocumentId, context.Message.Version);

                await RespondWithError(context, ex.Message);
            }
        }

        // Clean separation of concerns
        private async Task<DocumentUpdateInfo> GetDocumentInfoForUpdate(string documentId, string version)
        {
            try
            {
                var document = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.DocumentFile.Id == documentId && dv.VersionName == version,
                        include: i => i.Include(dv => dv.DocumentFile)
                    );

                if (document == null) return null;

                return new DocumentUpdateInfo
                {
                    DocumentVersion = document,
                    CurrentStatus = document.Status.ToString(),
                    VersionId = Guid.Parse(document.Id)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document info for {DocId}/{Version}", documentId, version);
                return null;
            }
        }

        private async Task<KernelMemoryResult> HandleKernelMemoryUpdate(
            DocumentUpdateInfo documentInfo,
            string newStatus,
            bool shouldUpdateKM,
            DateTime vietnamTime)
        {
            if (!shouldUpdateKM)
            {
                return new KernelMemoryResult { Updated = false, Error = null };
            }

            try
            {
                // Use ORIGINAL status for logic decision
                var shouldRemoveFromKM = documentInfo.CurrentStatus == "Approved" && newStatus == "Archived";

                if (shouldRemoveFromKM)
                {
                    _logger.LogInformation("Removing document {VersionId} from Kernel Memory (status change: {OldStatus} -> {NewStatus})",
                        documentInfo.VersionId, documentInfo.CurrentStatus, newStatus);

                    await RemoveFromKernelMemory(documentInfo.VersionId, vietnamTime);
                    return new KernelMemoryResult { Updated = true, Error = null };
                }
                else
                {
                    _logger.LogInformation("No Kernel Memory update needed for status change: {OldStatus} -> {NewStatus}",
                        documentInfo.CurrentStatus, newStatus);
                    return new KernelMemoryResult { Updated = false, Error = null };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kernel Memory operation failed for document {VersionId}, but document status was updated successfully",
                    documentInfo.VersionId);

                return new KernelMemoryResult { Updated = false, Error = ex.Message };
            }
        }

        private async Task RemoveFromKernelMemory(Guid versionId, DateTime vietnamTime)
        {
            var versionKmId = versionId.ToString();

            try
            {
                _logger.LogInformation("Starting Kernel Memory removal for document {VersionId}", versionId);

                // Increase timeout and add better error handling
                await _memory.DeleteDocumentAsync(versionKmId).WaitAsync(TimeSpan.FromSeconds(30));

                _logger.LogInformation("Successfully removed document {VersionId} from Kernel Memory", versionId);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout removing document {VersionId} from Kernel Memory after 30 seconds", versionId);
                throw new InvalidOperationException($"Kernel Memory operation timed out for document {versionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove document {VersionId} from Kernel Memory", versionId);
                throw new InvalidOperationException($"Kernel Memory operation failed: {ex.Message}");
            }
        }

        private async Task RespondWithError(ConsumeContext<UpdateDocumentStatusCommand> context, string errorMessage)
        {
            await context.RespondAsync(new UpdateDocumentStatusResponse
            {
                Success = false,
                ErrorMessage = errorMessage,
                DocumentId = context.Message.DocumentId,
                Version = context.Message.Version,
                KernelMemoryUpdated = false,
                RequestId = context.Message.RequestId
            });
        }

        // Supporting classes for clean data flow
        private class DocumentUpdateInfo
        {
            public DocumentVersion DocumentVersion { get; set; }
            public string CurrentStatus { get; set; }
            public Guid VersionId { get; set; }
        }

        private class KernelMemoryResult
        {
            public bool Updated { get; set; }
            public string Error { get; set; }
        }
    }
}
