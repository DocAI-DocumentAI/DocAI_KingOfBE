using AutoMapper;
using Notification.API.Payload.Response;
using Notification.API.Services.Interfaces;
using Notification.Domain.Enums;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;

namespace Notification.API.Services.Implement
{
    public class DocumentScanService : IDocumentScanService
    {
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly ILogger<DocumentScanService> _logger;
        private readonly IDocumentClient _documentClient;
        private readonly INotificationService _notificationService;
        private readonly INotificationConfigService _configService;


        public DocumentScanService(
            IUnitOfWork<NotificationDbContext> unitOfWork,
            ILogger<DocumentScanService> logger,
            IDocumentClient documentClient,
            INotificationService notificationService,
            INotificationConfigService configService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _documentClient = documentClient;
            _notificationService = notificationService;
            _configService = configService;
        }


        public async Task ScanAndProcessDocumentsAsync()
        {
            _logger.LogInformation("Starting document expiration scan at {Time}", DateTime.UtcNow);

            var config = await _configService.GetNotificationConfigAsync();
            if (!config.QuartzEnabled)
            {
                _logger.LogWarning("Document scanning is disabled in the system configuration.");
                return;
            }

            var warningDate = DateTime.UtcNow.AddDays(config.WarningThresholdDays);

            // Quy tắc "thay thế tài liệu": Giả định rằng DocumentClient đủ thông minh
            // để chỉ trả về các phiên bản MỚI NHẤT và đang CÓ HIỆU LỰC (Approved).
            // Do đó, các phiên bản cũ đã bị thay thế sẽ không nằm trong danh sách này.
            var documentsToCheck = await _documentClient.GetDocumentsForExpirationCheckAsync(warningDate);

            if (!documentsToCheck.Any())
            {
                _logger.LogInformation("No documents require expiration notifications at this time.");
                return;
            }

            _logger.LogInformation("Found {Count} relevant document versions to check.", documentsToCheck.Count);
            var logRepo = _unitOfWork.GetRepository<NotificationLog>();

            foreach (var doc in documentsToCheck)
            {
                // --- QUY TẮC NGHIỆP VỤ QUAN TRỌNG NHẤT ---
                // Trước khi làm bất cứ điều gì, hãy kiểm tra xem phiên bản tài liệu này
                // đã bị người dùng "Bỏ qua" (dismiss) hay chưa.
                bool isDismissed = await logRepo.AnyAsync(l =>
                    l.DocumentId == doc.DocumentId &&
                    l.DocumentVersion == doc.Version &&
                    l.IsDismissed == true
                );

                if (isDismissed)
                {
                    _logger.LogInformation("Skipping document version {DocId}/{Version} because it has been dismissed by a user.", doc.DocumentId, doc.Version);
                    continue; // Bỏ qua và chuyển đến tài liệu tiếp theo
                }

                // Nếu chưa bị bỏ qua, tiếp tục logic như cũ
                if (doc.EffectiveUntil.HasValue)
                {
                    if (doc.EffectiveUntil.Value.Date <= DateTime.UtcNow.Date)
                    {
                        await _notificationService.ProcessExpiredDocumentNotification(doc);
                    }
                    else if (doc.EffectiveUntil.Value.Date <= warningDate.Date)
                    {
                        await _notificationService.ProcessNearingExpirationNotification(doc);
                    }
                }
            }

            _logger.LogInformation("Finished document expiration scan.");
        }
    }
}
