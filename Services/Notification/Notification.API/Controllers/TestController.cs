using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Notification.API.Services.Interfaces;
using Notification.Domain.Enums;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Interfaces;
using Quartz;
using Shared.Command;
using Shared.DTOs;
using Shared.Models;

namespace Notification.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IDocumentScanService _scanService;
        private readonly INotificationService _notificationService;
        private readonly INotificationLogService _logService;
        private readonly IUnitOfWork<NotificationDbContext> _unitOfWork;
        private readonly IRequestClient<UpdateDocumentStatusCommand> _updateDocumentClient;
        private readonly IRequestClient<GetExpiringDocumentsCommand> _getDocumentsClient;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<TestController> _logger;

        public TestController(
            IDocumentScanService scanService,
            INotificationService notificationService,
            INotificationLogService logService,
            IUnitOfWork<NotificationDbContext> unitOfWork,
            IRequestClient<UpdateDocumentStatusCommand> updateDocumentClient,
            IRequestClient<GetExpiringDocumentsCommand> getDocumentsClient,
            ISchedulerFactory schedulerFactory,
            ILogger<TestController> logger)
        {
            _scanService = scanService;
            _notificationService = notificationService;
            _logService = logService;
            _unitOfWork = unitOfWork;
            _updateDocumentClient = updateDocumentClient;
            _getDocumentsClient = getDocumentsClient;
            _schedulerFactory = schedulerFactory;
            _logger = logger;
        }

        /// <summary>
        /// Test 1: Manual trigger document scan
        /// </summary>
        [HttpPost("trigger-scan")]
        public async Task<IActionResult> TriggerDocumentScan()
        {
            try
            {
                _logger.LogInformation("Manual document scan triggered via test controller");
                await _scanService.ScanAndProcessDocumentsAsync();
                return Ok(new { message = "Document scan completed successfully", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in manual document scan");
                return BadRequest(new { error = ex.Message, timestamp = DateTime.UtcNow });
            }
        }

        /// <summary>
        /// Test 2: Test document status update to Archived
        /// </summary>
        [HttpPost("test-update-status")]
        public async Task<IActionResult> TestUpdateDocumentStatus([FromBody] TestUpdateStatusRequest request)
        {
            try
            {
                _logger.LogInformation("Testing document status update for {DocumentId}/{Version} to {NewStatus}",
                    request.DocumentId, request.Version, request.NewStatus);

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                var response = await _updateDocumentClient.GetResponse<UpdateDocumentStatusResponse>(
                    new UpdateDocumentStatusCommand
                    {
                        DocumentId = request.DocumentId,
                        Version = request.Version,
                        NewStatus = request.NewStatus,
                        RequestId = Guid.NewGuid()
                    },
                    timeout.Token
                );

                return Ok(new
                {
                    success = response.Message.Success,
                    errorMessage = response.Message.ErrorMessage,
                    requestId = response.Message.RequestId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing document status update");
                return BadRequest(new { error = ex.Message, timestamp = DateTime.UtcNow });
            }
        }

        /// <summary>
        /// Test 3: Test getting expiring documents
        /// </summary>
        [HttpGet("test-expiring-documents")]
        public async Task<IActionResult> TestGetExpiringDocuments([FromQuery] int warningDays = 7)
        {
            try
            {
                var warningDate = DateTime.UtcNow.AddDays(warningDays);
                _logger.LogInformation("Testing get expiring documents before {WarningDate}", warningDate);

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                var response = await _getDocumentsClient.GetResponse<GetExpiringDocumentsResponse>(
                    new GetExpiringDocumentsCommand { WarningDate = warningDate },
                    timeout.Token
                );

                return Ok(new
                {
                    success = response.Message.Success,
                    documentCount = response.Message.Documents?.Count ?? 0,
                    documents = response.Message.Documents?.Take(5), // Chỉ show 5 documents đầu
                    errorMessage = response.Message.ErrorMessage,
                    warningDate = warningDate,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing get expiring documents");
                return BadRequest(new { error = ex.Message, timestamp = DateTime.UtcNow });
            }
        }

        /// <summary>
        /// Test 4: Test notification for specific document (with proper date validation)
        /// </summary>
        [HttpPost("test-notification")]
        public async Task<IActionResult> TestNotification([FromBody] TestNotificationRequest request)
        {
            try
            {
                _logger.LogInformation("Testing notification for document {DocumentId}/{Version}",
                    request.DocumentId, request.Version);

                // ✅ Validate: Chỉ test documents có cả EffectiveFrom và EffectiveUntil
                if (!request.EffectiveFrom.HasValue || !request.EffectiveUntil.HasValue)
                {
                    return BadRequest(new
                    {
                        error = "Both EffectiveFrom and EffectiveUntil are required for expiration testing",
                        timestamp = DateTime.UtcNow
                    });
                }

                var testDocument = new DocumentExpirationDto
                {
                    DocumentId = request.DocumentId,
                    Title = request.Title ?? "Test Document",
                    Version = request.Version,
                    DepartmentId = request.DepartmentId,
                    DepartmentName = request.DepartmentName ?? "Test Department",
                    EffectiveFrom = request.EffectiveFrom,    // ✅ THÊM: EffectiveFrom
                    EffectiveUntil = request.EffectiveUntil,  // ✅ Required
                    Status = "Approved",
                    IsPublic = request.IsPublic,
                    CreatedBy = request.CreatedBy
                };

                // ✅ Logic: Xác định loại notification dựa trên ngày hết hạn
                var today = DateTime.UtcNow.Date;
                var effectiveUntilDate = request.EffectiveUntil.Value.Date;

                bool isActuallyExpired = effectiveUntilDate <= today;
                bool isNearExpiration = effectiveUntilDate > today && effectiveUntilDate <= today.AddDays(7);

                string notificationType;
                if (request.ForceExpired || isActuallyExpired)
                {
                    await _notificationService.ProcessExpiredDocumentNotification(testDocument);
                    notificationType = "Expired/Archived";
                }
                else if (isNearExpiration)
                {
                    await _notificationService.ProcessNearingExpirationNotification(testDocument);
                    notificationType = "NearingExpiration";
                }
                else
                {
                    return BadRequest(new
                    {
                        error = $"Document is not near expiration. EffectiveUntil: {effectiveUntilDate:yyyy-MM-dd}, Today: {today:yyyy-MM-dd}",
                        effectiveUntil = effectiveUntilDate,
                        today = today,
                        daysUntilExpiration = (effectiveUntilDate - today).Days,
                        timestamp = DateTime.UtcNow
                    });
                }

                return Ok(new
                {
                    message = $"Test notification sent for document {request.DocumentId}",
                    notificationType = notificationType,
                    effectiveFrom = request.EffectiveFrom,
                    effectiveUntil = request.EffectiveUntil,
                    daysUntilExpiration = (effectiveUntilDate - today).Days,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing notification");
                return BadRequest(new { error = ex.Message, timestamp = DateTime.UtcNow });
            }
        }

        /// <summary>
        /// Test 6: Check notification logs (simplified - no dismiss info)
        /// </summary>
        [HttpGet("notification-logs")]
        public async Task<IActionResult> GetNotificationLogs([FromQuery] string? documentId = null,
            [FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            try
            {
                var request = new Notification.API.Payload.Request.NotificationRequest
                {
                    DocumentId = documentId,
                    Page = page,
                    Size = Math.Min(size, 50),
                    SortBy = "CreateAt",
                    IsAsc = false
                };

                var logs = await _logService.GetNotificationLogsAsync(request);

                return Ok(new
                {
                    pageCount = logs.TotalPages,
                    logs = logs.Items.Select(log => new
                    {
                        id = log.Id,
                        documentId = log.DocumentId,
                        documentVersion = log.DocumentVersion,
                        notificationType = log.NotificationType,
                        recipientAddress = log.RecipientAddress,
                        subject = log.Subject,
                        isSent = log.IsSent,
                        sentAt = log.SentAt,
                        errorMessage = log.ErrorMessage,
                        createdAt = log.CreateAt
                        // ✅ REMOVED: dismiss-related fields
                    }),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification logs");
                return BadRequest(new { error = ex.Message, timestamp = DateTime.UtcNow });
            }
        }

        /// <summary>
        /// Test 6: Check duplicate notification logic
        /// </summary>
        [HttpGet("check-duplicates/{documentId}/{version}")]
        public async Task<IActionResult> CheckDuplicateNotifications(string documentId, string version)
        {
            try
            {
                var logRepo = _unitOfWork.GetRepository<NotificationLog>();
                var last24Hours = DateTime.UtcNow.AddHours(-24);

                var recentNotifications = await logRepo.GetListAsync(
                    predicate: l => l.DocumentId == documentId &&
                                   l.DocumentVersion == version &&
                                   l.SentAt >= last24Hours,
                    orderBy: o => o.OrderByDescending(l => l.SentAt)
                );

                var isDismissed = await logRepo.AnyAsync(l =>
                    l.DocumentId == documentId &&
                    l.DocumentVersion == version);

                return Ok(new
                {
                    documentId,
                    version,
                    isDismissed,
                    recentNotificationsCount = recentNotifications.Count,
                    recentNotifications = recentNotifications.Select(n => new
                    {
                        type = n.NotificationType.ToString(),
                        recipient = n.RecipientAddress,
                        sentAt = n.SentAt,
                        isSent = n.IsSent,
                    }),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking duplicate notifications");
                return BadRequest(new { error = ex.Message, timestamp = DateTime.UtcNow });
            }
        }

        /// <summary>
        /// Test 7: Force trigger notification scan job
        /// </summary>
        [HttpPost("trigger-job")]
        public async Task<IActionResult> TriggerNotificationJob()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                var jobKey = new JobKey("NotificationScanJob");

                var isRunning = await scheduler.CheckExists(jobKey);
                if (!isRunning)
                {
                    return BadRequest(new { error = "NotificationScanJob is not scheduled", timestamp = DateTime.UtcNow });
                }

                // Trigger job immediately
                await scheduler.TriggerJob(jobKey);

                return Ok(new
                {
                    message = "NotificationScanJob triggered successfully",
                    jobKey = jobKey.ToString(),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering notification job");
                return BadRequest(new { error = ex.Message, timestamp = DateTime.UtcNow });
            }
        }

        /// <summary>
        /// Test 8: Get job status
        /// </summary>
        [HttpGet("job-status")]
        public async Task<IActionResult> GetJobStatus()
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler();
                var jobKey = new JobKey("NotificationScanJob");

                var jobDetail = await scheduler.GetJobDetail(jobKey);
                if (jobDetail == null)
                {
                    return NotFound(new { error = "NotificationScanJob not found", timestamp = DateTime.UtcNow });
                }

                var triggers = await scheduler.GetTriggersOfJob(jobKey);
                var isRunning = await scheduler.CheckExists(jobKey);

                // ✅ FIX: Safe string retrieval với default values
                var lastSuccessfulRun = jobDetail.JobDataMap.ContainsKey("LastSuccessfulRun")
                    ? jobDetail.JobDataMap.GetString("LastSuccessfulRun")
                    : "Never";

                var lastErrorTime = jobDetail.JobDataMap.ContainsKey("LastErrorTime")
                    ? jobDetail.JobDataMap.GetString("LastErrorTime")
                    : "None";

                var lastError = jobDetail.JobDataMap.ContainsKey("LastError")
                    ? jobDetail.JobDataMap.GetString("LastError")
                    : "None";

                var lastRunDuration = jobDetail.JobDataMap.ContainsKey("LastRunDuration")
                    ? jobDetail.JobDataMap.GetString("LastRunDuration")
                    : "Unknown";

                return Ok(new
                {
                    jobKey = jobKey.ToString(),
                    isScheduled = isRunning,
                    lastSuccessfulRun = lastSuccessfulRun,
                    lastErrorTime = lastErrorTime,
                    lastError = lastError,
                    lastRunDuration = lastRunDuration,
                    triggerCount = triggers.Count(),
                    nextFireTime = triggers.FirstOrDefault()?.GetNextFireTimeUtc()?.ToString(),
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting job status");
                return BadRequest(new { error = ex.Message, timestamp = DateTime.UtcNow });
            }
        }
    }

    // Request DTOs
    public class TestUpdateStatusRequest
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string NewStatus { get; set; } = "Archived"; // ✅ SỬA: Default thành Archived
    }

    public class TestNotificationRequest
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? Title { get; set; }
        public Guid DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public DateTime? EffectiveFrom { get; set; }  // ✅ THÊM: Required cho test
        public DateTime? EffectiveUntil { get; set; }
        public bool IsPublic { get; set; }
        public string? CreatedBy { get; set; }
        public bool ForceExpired { get; set; } = false; // ✅ THÊM: Override logic để force test expired
    }
}