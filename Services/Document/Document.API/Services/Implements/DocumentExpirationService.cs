using AutoMapper;
using Document.API.Services.Interfaces;
using Document.Domain.Enums;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace Document.API.Services.Implements
{
    public class DocumentExpirationService : IDocumentExpirationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<DocumentExpirationService> _logger;

        public DocumentExpirationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<DocumentExpirationService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<List<DocumentExpirationDto>> GetExpiringDocumentsAsync(DateTime warningDate)
        {
            _logger.LogInformation("Getting documents for expiration check before {WarningDate}", warningDate);

            var expiringDocuments = await _unitOfWork.GetRepository<DocumentVersion>()
                .GetListAsync(
                    predicate: dv =>
                        dv.Status == StatusEnum.Approved &&
                        dv.IsOfficial &&
                        dv.EffectiveUntil.HasValue &&
                        dv.EffectiveUntil.Value <= warningDate,
                    include: i => i.Include(dv => dv.DocumentFile)
                                   .ThenInclude(df => df.DocumentType)
                );

            var result = new List<DocumentExpirationDto>();

            foreach (var doc in expiringDocuments)
            {
                result.Add(new DocumentExpirationDto
                {
                    DocumentId = Guid.Parse(doc.DocumentFile.Id),
                    Title = doc.Title,
                    Version = doc.VersionName,
                    DepartmentId = Guid.Parse(doc.DocumentFile.DepartmentId),
                    EffectiveUntil = doc.EffectiveUntil,
                    Status = doc.Status.ToString(),
                    DocumentLink = GenerateDocumentLink(doc.DocumentFile.Id, doc.Id),
                    IsPublic = doc.IsPublic,
                    CreatedBy = doc.DocumentFile.CreatedBy
                });
            }

            _logger.LogInformation("Found {Count} documents for expiration notification", result.Count);
            return result;
        }

        public async Task<bool> UpdateDocumentStatusAsync(Guid documentId, string version, string newStatus)
        {
            _logger.LogInformation("Updating document {DocumentId} version {Version} to status {NewStatus}",
                documentId, version, newStatus);

            try
            {
                var document = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.DocumentFile.Id == documentId.ToString() &&
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
                    document.Status = status;
                    document.LastUpdatedTime = DateTime.UtcNow;
                    document.LastUpdatedBy = "system_notification";

                    _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(document);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Successfully updated document {DocumentId} to status {NewStatus}",
                        documentId, newStatus);
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
                _logger.LogError(ex, "Error updating document status for {DocumentId}", documentId);
                return false;
            }
        }

        public async Task<bool> DeactivateDocumentWarningsAsync(Guid documentId, string version)
        {
            _logger.LogInformation("Deactivating warnings for document {DocumentId} version {Version}",
                documentId, version);

            try
            {
                // Business logic to deactivate warnings
                // This could involve updating a flag or creating a record
                var document = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.DocumentFile.Id == documentId.ToString() &&
                                        dv.VersionName == version
                    );

                if (document != null)
                {
                    // Add custom logic here if needed
                    // For example, setting a flag or updating metadata

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
            return $"/documents/{documentId}/versions/{versionId}";
        }
    }
}
