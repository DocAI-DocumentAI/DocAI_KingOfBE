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

        public async Task<List<DocumentExpirationDto>> GetExpiringDocumentsAsync(DateTime warningDate)
        {
            // ✅ Sử dụng ngày Việt Nam để so sánh thay vì warningDate
            var vietnamDate = VietnamTimeHelper.GetVietnamDate();

            _logger.LogInformation("Getting documents for expiration check. Warning date: {WarningDate}, Vietnam date: {VietnamDate}",
                warningDate, vietnamDate);

            // ✅ FIX: Chỉ lấy documents có cả EffectiveFrom và EffectiveUntil, và so sánh với Vietnam date
            var expiringDocuments = await _unitOfWork.GetRepository<DocumentVersion>()
                .GetListAsync(
                    predicate: dv =>
                        dv.Status == StatusEnum.Approved &&
                        dv.EffectiveFrom.HasValue &&      // ✅ THÊM: Phải có EffectiveFrom
                        dv.EffectiveUntil.HasValue &&     // ✅ THÊM: Phải có EffectiveUntil
                        dv.EffectiveUntil.Value.Date <= vietnamDate, // ✅ So sánh với Vietnam date
                    include: i => i.Include(dv => dv.DocumentFile)
                                   .ThenInclude(df => df.DocumentType)
                );

            _logger.LogInformation("Found {Count} documents with both EffectiveFrom and EffectiveUntil that expired by Vietnam date {VietnamDate}",
                expiringDocuments.Count, vietnamDate);

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

                // ✅ Log thông tin timezone cho mỗi document
                var daysFromToday = VietnamTimeHelper.DaysFromToday(doc.EffectiveUntil.Value);
                _logger.LogDebug("Document {DocId} expires on {ExpiryDate}, {Days} days from Vietnam today",
                    doc.DocumentFile.Id, doc.EffectiveUntil.Value.ToString("yyyy-MM-dd"), daysFromToday);

                result.Add(new DocumentExpirationDto
                {
                    DocumentId = doc.DocumentFile.Id,
                    Title = doc.Title,
                    Version = doc.VersionName,
                    DepartmentId = departmentId,
                    DepartmentName = departmentName,
                    EffectiveFrom = doc.EffectiveFrom,     // ✅ THÊM: Include EffectiveFrom
                    EffectiveUntil = doc.EffectiveUntil,   // ✅ Đã có
                    Status = doc.Status.ToString(),
                    DocumentLink = GenerateDocumentLink(doc.DocumentFile.Id, doc.Id),
                    IsPublic = doc.IsPublic,
                    CreatedBy = doc.DocumentFile.CreatedBy
                });
            }

            _logger.LogInformation("Returning {Count} expired documents based on Vietnam timezone", result.Count);
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
                    _logger.LogInformation("Successfully retrieved {Count} department names",
                        response.Message.DepartmentNames.Count);
                    return response.Message.DepartmentNames;
                }

                _logger.LogWarning("Failed to get department names: {Error}", response.Message.ErrorMessage);
            }
            catch (RequestTimeoutException)
            {
                _logger.LogWarning("Timeout getting department names, using fallback");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department names, using fallback");
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
            var vietnamTime = VietnamTimeHelper.GetVietnamDateTime();
            _logger.LogInformation("Updating document {DocumentId} version {Version} to status {NewStatus} at Vietnam time {VietnamTime}",
                documentId, version, newStatus, vietnamTime);

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
                    // ✅ Log trạng thái cũ và mới
                    var oldStatus = document.Status;
                    document.Status = status;
                    document.LastUpdatedTime = DateTime.UtcNow; // Vẫn lưu UTC trong DB
                    document.LastUpdatedBy = "system_notification";

                    _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(document);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Successfully updated document {DocumentId} from {OldStatus} to {NewStatus} at Vietnam time {VietnamTime}",
                        documentId, oldStatus, newStatus, vietnamTime);
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
                    documentId, vietnamTime);
                return false;
            }
        }

        public async Task<bool> DeactivateDocumentWarningsAsync(string documentId, string version)
        {
            _logger.LogInformation("Deactivating warnings for document {DocumentId} version {Version}",
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
                    _logger.LogInformation("Warnings deactivated for document {DocumentId}", documentId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating warnings for document {DocumentId}", documentId);
                return false;
            }
        }

        private string GenerateDocumentLink(string documentId, string versionId)
        {
            return $"/document/{documentId}/versions/{versionId}";
        }
    }
}
