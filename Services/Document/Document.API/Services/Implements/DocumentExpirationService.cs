using AutoMapper;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Enums;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using DocumentFormat.OpenXml.Office2010.Word;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Command;
using Shared.DTOs;
using Shared.Models;
using Shared.Utils;

namespace Document.API.Services.Implements
{
    public class DocumentExpirationService : IDocumentExpirationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<DocumentExpirationService> _logger;
        private readonly IRequestClient<GetDepartmentNamesCommand> _departmentClient;
        private readonly IRequestClient<GetUserByIdCommand> _userClient;

        public DocumentExpirationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<DocumentExpirationService> logger,
            IRequestClient<GetDepartmentNamesCommand> departmentClient,
            IRequestClient<GetUserByIdCommand> userClient)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _departmentClient = departmentClient;
            _userClient = userClient;
        }
        public async Task<List<DocumentExpirationDto>> GetDocumentsRequiringStatusUpdateAsync()
        {
            var todayUtc = TimeZoneHelper.UtcToday;

            _logger.LogInformation("Getting documents requiring status update to Archived. Today UTC: {TodayUtc}", todayUtc);

            try
            {
                // Get documents that are Approved and expired today
                var documentsToUpdate = await _unitOfWork.GetRepository<DocumentVersion>()
                    .GetListAsync(
                        predicate: dv =>
                            dv.Status == StatusEnum.Approved &&
                            dv.EffectiveFrom.HasValue &&
                            dv.EffectiveUntil.HasValue &&
                            dv.EffectiveUntil.Value.Date <= todayUtc.Date, // Documents that expired today or before
                        include: i => i.Include(dv => dv.DocumentFile)
                                       .ThenInclude(df => df.DocumentType)
                    );

                var result = new List<DocumentExpirationDto>();
                var departmentIds = documentsToUpdate
                    .Select(doc => doc.DocumentFile.DepartmentId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList();

                var departmentNames = await GetDepartmentNamesAsync(departmentIds);

                foreach (var doc in documentsToUpdate)
                {
                    // Double-check with TimeZoneHelper to ensure accuracy
                    var daysFromToday = TimeZoneHelper.DaysFromTodayFromDatabase(doc.EffectiveUntil.Value);

                    // Only include documents that are actually expired (daysFromToday <= 0)
                    if (daysFromToday > 0) continue;

                    var departmentId = Guid.TryParse(doc.DocumentFile.DepartmentId, out var deptId) ? deptId : Guid.Empty;
                    var departmentName = departmentNames.TryGetValue(departmentId, out var name) ? name : "Unknown Department";

                    result.Add(new DocumentExpirationDto
                    {
                        DocumentId = doc.DocumentFile.Id,
                        Title = doc.Title,
                        Version = doc.VersionName,
                        DepartmentId = departmentId,
                        DepartmentName = departmentName,
                        EffectiveFrom = doc.EffectiveFrom,
                        EffectiveUntil = doc.EffectiveUntil,
                        Status = doc.Status.ToString(),
                        DocumentLink = GenerateDocumentLink(doc.DocumentFile.Id, doc.Id),
                        IsPublic = doc.IsPublic,
                        CreatedBy = doc.DocumentFile.CreatedBy
                    });
                }

                _logger.LogInformation("Found {Count} documents requiring status update to Archived", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents requiring status update");
                return new List<DocumentExpirationDto>();
            }
        }
        public async Task<List<DocumentExpirationDto>> GetExpiringDocumentsAsync(DateTime warningDate)
        {

            // Ensure warningDate is UTC for PostgreSQL compatibility
            var warningDateUtc = TimeZoneHelper.EnsureUtc(warningDate);
            var todayUtc = TimeZoneHelper.UtcToday;

            _logger.LogInformation("Getting documents for expiration check. Warning date UTC: {WarningDateUtc}, Today UTC: {TodayUtc}",
                warningDateUtc, todayUtc);

            // Query database with UTC dates to prevent PostgreSQL timezone errors
            var expiringDocuments = await _unitOfWork.GetRepository<DocumentVersion>()
                .GetListAsync(
                    predicate: dv =>
                        dv.Status == StatusEnum.Approved &&
                        dv.EffectiveFrom.HasValue &&
                        dv.EffectiveUntil.HasValue &&
                        dv.EffectiveUntil.Value.Date <= warningDateUtc.Date, // Safe UTC comparison
                    include: i => i.Include(dv => dv.DocumentFile)
                                   .ThenInclude(df => df.DocumentType)
                );

            _logger.LogInformation("Found {Count} documents with expiration dates before {WarningDateUtc}",
                expiringDocuments.Count, warningDateUtc);

            var result = new List<DocumentExpirationDto>();

            // Get unique department IDs
            var departmentIds = expiringDocuments
                .Select(doc => doc.DocumentFile.DepartmentId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            // Get department names from Auth service
            var departmentNames = await GetDepartmentNamesAsync(departmentIds);

            foreach (var doc in expiringDocuments)
            {
                var departmentId = Guid.TryParse(doc.DocumentFile.DepartmentId, out var deptId) ? deptId : Guid.Empty;
                var departmentName = departmentNames.TryGetValue(departmentId, out var name) ? name : "Unknown Department";

                // Use TimeZoneHelper for consistent date calculations
                var daysFromToday = TimeZoneHelper.DaysFromTodayFromDatabase(doc.EffectiveUntil.Value);
                var vietnamExpiryDate = TimeZoneHelper.ConvertUtcToVietnam(
                    DateTime.SpecifyKind(doc.EffectiveUntil.Value, DateTimeKind.Utc));

                _logger.LogDebug("Document {DocId} expires on {ExpiryDateVietnam} (Vietnam time), {Days} days from today",
                    doc.DocumentFile.Id, vietnamExpiryDate.ToString("yyyy-MM-dd"), daysFromToday);

                result.Add(new DocumentExpirationDto
                {
                    DocumentId = doc.DocumentFile.Id,
                    Title = doc.Title,
                    Version = doc.VersionName,
                    DepartmentId = departmentId,
                    DepartmentName = departmentName,
                    EffectiveFrom = doc.EffectiveFrom,
                    EffectiveUntil = doc.EffectiveUntil, // Keep original UTC values
                    Status = doc.Status.ToString(),
                    DocumentLink = GenerateDocumentLink(doc.DocumentFile.Id, doc.Id),
                    IsPublic = doc.IsPublic,
                    CreatedBy = doc.DocumentFile.CreatedBy
                });
            }

            _logger.LogInformation("Returning {Count} expiring/expired documents", result.Count);
            return result;
        }
        private async Task<Dictionary<Guid, string>> GetDepartmentNamesAsync(List<Guid> departmentIds)
        {
            if (!departmentIds.Any())
                return new Dictionary<Guid, string>();

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                var response = await _departmentClient.GetResponse<GetDepartmentNamesResponse>(
                    new GetDepartmentNamesCommand { DepartmentIds = departmentIds },
                    timeout.Token
                );

                if (response.Message.Success)
                {
                    _logger.LogDebug("✅ Successfully retrieved {Count} department names",
                        response.Message.DepartmentNames.Count);
                    return response.Message.DepartmentNames;
                }

                _logger.LogWarning("⚠️ Failed to get department names: {Error}", response.Message.ErrorMessage);
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("⏰ Timeout getting department names, using fallback");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting department names, using fallback");
            }

            // Fallback: Use department ID as name
            return departmentIds.ToDictionary(
                id => id,
                id => $"Department-{id.ToString()[..8]}"
            );
        }

        // ... rest of the methods stay the same
        public async Task<bool> UpdateDocumentStatusAsync(string documentId, string version, string newStatus)
        {
            var vietnamTime = TimeZoneHelper.VietnamNow; // For display/logging
            var utcNow = TimeZoneHelper.UtcNow; // For database storage - PostgreSQL safe

            _logger.LogInformation("Updating document {DocumentId} version {Version} to status {NewStatus} at Vietnam time {VietnamTime}",
                documentId, version, newStatus, vietnamTime.ToString("yyyy-MM-dd HH:mm:ss"));

            try
            {
                var document = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.DocumentFile.Id == documentId &&
                                        dv.VersionName == version,
                        include: i => i.Include(dv => dv.DocumentFile)
                    );

                if (document == null)
                {
                    _logger.LogWarning("Document not found for update: {DocumentId}/{Version}", documentId, version);
                    return false;
                }

                if (Enum.TryParse<StatusEnum>(newStatus, out var status))
                {
                    var oldStatus = document.Status;
                    document.Status = status;
                    document.LastUpdatedTime = utcNow; // Always store UTC in database
                    document.LastUpdatedBy = "system_notification";

                    await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(document);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Successfully updated document {DocumentId} from {OldStatus} to {NewStatus} at Vietnam time {VietnamTime}",
                        documentId, oldStatus, newStatus, vietnamTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    return true;
                }
                else
                {
                    _logger.LogError("Invalid status provided: {NewStatus}", newStatus);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document status for {DocumentId} at Vietnam time {VietnamTime}",
                    documentId, vietnamTime.ToString("yyyy-MM-dd HH:mm:ss"));
                return false;
            }
        }

        public async Task<bool> DeactivateDocumentWarningsAsync(string documentId, string version)
        {
            _logger.LogInformation("🔕 Deactivating warnings for document {DocumentId} version {Version}",
                        documentId, version);

            try
            {
                var document = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.DocumentFile.Id == documentId &&
                                        dv.VersionName == version
                    );

                if (document != null)
                {
                    _logger.LogInformation("✅ Warnings deactivated for document {DocumentId}", documentId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deactivating warnings for document {DocumentId}", documentId);
                return false;
            }
        }

        private string GenerateDocumentLink(string documentId, string versionId)
        {
            return $"/document/{documentId}/version/{versionId}";
        }
    }
}
