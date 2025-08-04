using AutoMapper;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.Domain.Model;
using Document.Domain.Models;
using Document.Infrastructure.Paginate;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service implementation for managing document types
    /// </summary>
    public class DocumentTypeService : IDocumentTypeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<DocumentTypeService> _logger;

        public DocumentTypeService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<DocumentTypeService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new document type
        /// </summary>
        /// <param name="request">The document type creation request</param>
        /// <param name="userId">The ID of the user creating the document type</param>
        /// <returns>The created document type response</returns>
        public async Task<DocumentTypeResponse> CreateDocumentTypeAsync(CreateDocumentTypeRequest request, string userId)
        {
            _logger.LogInformation("Creating new document type with name: {Name} by user: {UserId}", request.Name, userId);

            // Validate business rules
            await ValidateDocumentTypeBusinessRulesAsync(request.Name, request.Description);

            var documentType = new DocumentType
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                CreatedBy = userId,
                CreatedTime = DateTime.UtcNow
            };

            await _unitOfWork.GetRepository<DocumentType>().InsertAsync(documentType);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Successfully created document type with ID: {DocumentTypeId}", documentType.Id);

            return _mapper.Map<DocumentTypeResponse>(documentType);
        }

        /// <summary>
        /// Gets a document type by its ID
        /// </summary>
        /// <param name="documentTypeId">The ID of the document type</param>
        /// <returns>The document type response</returns>
        public async Task<DocumentTypeResponse> GetDocumentTypeByIdAsync(string documentTypeId)
        {
            _logger.LogInformation("Retrieving document type with ID: {DocumentTypeId}", documentTypeId);

            var documentType = await _unitOfWork.GetRepository<DocumentType>()
                .SingleOrDefaultAsync(
                    predicate: dt => dt.Id == documentTypeId,
                    include: i => i.Include(dt => dt.DocumentFiles)
                );

            if (documentType == null)
            {
                throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND,
                    MessageConstant.DocumentTypeNotFound);
            }

            var response = _mapper.Map<DocumentTypeResponse>(documentType);
            response.DocumentCount = documentType.DocumentFiles?.Count ?? 0;

            return response;
        }

        /// <summary>
        /// Gets all document types with pagination
        /// </summary>
        /// <param name="pageNumber">The page number</param>
        /// <param name="pageSize">The page size</param>
        /// <returns>Paginated list of document types</returns>
        public async Task<IPaginate<DocumentTypeResponse>> GetAllDocumentTypesAsync(int pageNumber, int pageSize)
        {
            _logger.LogInformation("Retrieving document types - Page: {PageNumber}, Size: {PageSize}", pageNumber, pageSize);

            var documentTypes = await _unitOfWork.GetRepository<DocumentType>().GetPagingListAsync(
                selector: dt => new DocumentTypeResponse
                {
                    Id = dt.Id,
                    Name = dt.Name,
                    Description = dt.Description,
                    CreatedBy = dt.CreatedBy,
                    LastUpdatedBy = dt.LastUpdatedBy,
                    CreatedTime = dt.CreatedTime,
                    LastUpdatedTime = dt.LastUpdatedTime,
                    DocumentCount = dt.DocumentFiles.Count
                },
                filter: null,
                predicate: dt => dt.DeletedTime == null, // Only get non-deleted document types
                include: i => i.Include(dt => dt.DocumentFiles),
                orderBy: q => q.OrderBy(dt => dt.Name),
                page: pageNumber,
                size: pageSize
            );

            return documentTypes;
        }

        /// <summary>
        /// Updates an existing document type
        /// </summary>
        /// <param name="documentTypeId">The ID of the document type to update</param>
        /// <param name="request">The document type update request</param>
        /// <param name="userId">The ID of the user updating the document type</param>
        /// <returns>The updated document type response</returns>
        public async Task<DocumentTypeResponse> UpdateDocumentTypeAsync(string documentTypeId, UpdateDocumentTypeRequest request, string userId)
        {
            _logger.LogInformation("Updating document type with ID: {DocumentTypeId} by user: {UserId}", documentTypeId, userId);

            var documentType = await _unitOfWork.GetRepository<DocumentType>()
                .SingleOrDefaultAsync(predicate: dt => dt.Id == documentTypeId && dt.DeletedTime == null);

            if (documentType == null)
            {
                throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND,
                    MessageConstant.DocumentTypeNotFound);
            }

            // Validate business rules (excluding current document type from uniqueness check)
            await ValidateDocumentTypeBusinessRulesAsync(request.Name, request.Description, documentTypeId);

            documentType.Name = request.Name.Trim();
            documentType.Description = request.Description?.Trim();
            documentType.LastUpdatedBy = userId;
            documentType.LastUpdatedTime = DateTime.UtcNow;

            _unitOfWork.GetRepository<DocumentType>().UpdateAsync(documentType);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Successfully updated document type with ID: {DocumentTypeId}", documentTypeId);

            return _mapper.Map<DocumentTypeResponse>(documentType);
        }

        /// <summary>
        /// Deletes a document type
        /// </summary>
        /// <param name="documentTypeId">The ID of the document type to delete</param>
        /// <param name="userId">The ID of the user deleting the document type</param>
        /// <returns>Task representing the async operation</returns>
        public async Task DeleteDocumentTypeAsync(string documentTypeId, string userId)
        {
            _logger.LogInformation("Deleting document type with ID: {DocumentTypeId} by user: {UserId}", documentTypeId, userId);

            var documentType = await _unitOfWork.GetRepository<DocumentType>()
                .SingleOrDefaultAsync(
                    predicate: dt => dt.Id == documentTypeId && dt.DeletedTime == null,
                    include: i => i.Include(dt => dt.DocumentFiles)
                );

            if (documentType == null)
            {
                throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND,
                    MessageConstant.DocumentTypeNotFound);
            }

            // Check if there are any documents associated with this document type
            if (documentType.DocumentFiles != null && documentType.DocumentFiles.Any())
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                    MessageConstant.DocumentTypeInUse);
            }

            // Soft delete
            documentType.DeletedBy = userId;
            documentType.DeletedTime = DateTime.UtcNow;

            _unitOfWork.GetRepository<DocumentType>().UpdateAsync(documentType);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Successfully deleted document type with ID: {DocumentTypeId}", documentTypeId);
        }

        /// <summary>
        /// Gets all document types as a simple list (for dropdown/selection purposes)
        /// </summary>
        /// <returns>List of document types</returns>
        public async Task<List<DocumentTypeResponse>> GetDocumentTypesListAsync()
        {
            _logger.LogInformation("Retrieving all document types for selection list");

            var documentTypes = await _unitOfWork.GetRepository<DocumentType>()
                .GetListAsync(
                    selector: dt => _mapper.Map<DocumentTypeResponse>(dt),
                    predicate: dt => dt.DeletedTime == null,
                    orderBy: q => q.OrderBy(dt => dt.Name)
                );

            return documentTypes.ToList();
        }

        /// <summary>
        /// Validates DocumentType business rules
        /// </summary>
        /// <param name="name">Document type name</param>
        /// <param name="description">Document type description</param>
        /// <param name="excludeId">ID to exclude from uniqueness check (for updates)</param>
        /// <returns>Task representing the async validation</returns>
        private async Task ValidateDocumentTypeBusinessRulesAsync(string name, string? description, string? excludeId = null)
        {
            // Validate name is not empty or whitespace
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST,
                    MessageConstant.DocumentTypeNameRequired);
            }

            // Validate name length
            if (name.Length > 100)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST,
                    MessageConstant.DocumentTypeNameTooLong);
            }

            // Validate description length if provided
            if (!string.IsNullOrEmpty(description) && description.Length > 500)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST,
                    MessageConstant.DocumentTypeDescriptionTooLong);
            }

            // Validate name uniqueness
            var existingDocumentType = await _unitOfWork.GetRepository<DocumentType>()
                .SingleOrDefaultAsync(predicate: dt => dt.Name.ToLower() == name.ToLower() &&
                                                      dt.DeletedTime == null &&
                                                      (excludeId == null || dt.Id != excludeId));

            if (existingDocumentType != null)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                    MessageConstant.DocumentTypeNameExists);
            }
        }
    }
}
