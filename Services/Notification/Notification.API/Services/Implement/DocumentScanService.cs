using AutoMapper;
using MassTransit;
using Notification.API.Constants;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Enums;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;
using Quartz.Impl.AdoJobStore;
using Shared.Command;
using Shared.DTOs;
using Shared.Models;

namespace Notification.API.Services.Implement
{
    public class DocumentScanService : IDocumentScanService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly ILogger<DocumentScanService> _logger;
        private readonly INotificationService _notificationService;
        private readonly INotificationConfigService _configService;
        private readonly IRequestClient<GetExpiringDocumentsCommand> _documentClient;

        public DocumentScanService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            ILogger<DocumentScanService> logger,
            INotificationService notificationService,
            INotificationConfigService configService,
            IRequestClient<GetExpiringDocumentsCommand> documentClient)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _notificationService = notificationService;
            _configService = configService;
            _documentClient = documentClient;
        }

        public async Task ScanAndProcessDocumentsAsync()
        {
            _logger.LogInformation("Starting document expiration scan");

            try
            {
                var config = await _configService.GetNotificationConfigAsync();
                if (!config.QuartzEnabled)
                {
                    _logger.LogInformation("Document scanning is disabled");
                    return;
                }

                var warningDate = DateTime.UtcNow.AddDays(config.WarningThresholdDays);
                var documents = await GetExpiringDocumentsAsync(warningDate);

                if (!documents.Any())
                {
                    _logger.LogInformation("No documents require expiration notifications");
                    return;
                }

                await ProcessDocumentsAsync(documents);
                _logger.LogInformation("Document expiration scan completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during document expiration scan");
            }
        }

        private async Task<List<DocumentExpirationDto>> GetExpiringDocumentsAsync(DateTime warningDate)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ApiConstants.EMAIL_TIMEOUT_SECONDS));

                var response = await _documentClient.GetResponse<GetExpiringDocumentsResponse>(
                    new GetExpiringDocumentsCommand { WarningDate = warningDate },
                    timeout.Token
                );

                if (response.Message.Success)
                {
                    _logger.LogInformation("Retrieved {Count} documents for expiration check", response.Message.Documents.Count);
                    return response.Message.Documents;
                }

                _logger.LogWarning("Document service returned error: {Error}", response.Message.ErrorMessage);
                return new List<DocumentExpirationDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expiring documents");
                return new List<DocumentExpirationDto>();
            }
        }

        private async Task ProcessDocumentsAsync(List<DocumentExpirationDto> documents)
        {
            var processedCount = 0;
            var skippedCount = 0;

            foreach (var doc in documents)
            {
                try
                {
                    if (await IsDocumentDismissedAsync(doc))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (doc.EffectiveUntil.HasValue)
                    {
                        var today = DateTime.UtcNow.Date;
                        var effectiveDate = doc.EffectiveUntil.Value.Date;

                        if (effectiveDate <= today)
                        {
                            await _notificationService.ProcessExpiredDocumentNotification(doc);
                        }
                        else if (effectiveDate <= today.AddDays(7))
                        {
                            await _notificationService.ProcessNearingExpirationNotification(doc);
                        }

                        processedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing document {DocId}/{Version}", doc.DocumentId, doc.Version);
                }
            }

            _logger.LogInformation("Processed {Processed} documents, skipped {Skipped}", processedCount, skippedCount);
        }

        private async Task<bool> IsDocumentDismissedAsync(DocumentExpirationDto doc)
        {
            try
            {
                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                return await logRepo.AnyAsync(l =>
                    l.DocumentId == doc.DocumentId &&
                    l.DocumentVersion == doc.Version &&
                    l.IsDismissed == true
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking dismissed status for {DocId}/{Version}", doc.DocumentId, doc.Version);
                return false;
            }
        }
    }
}
