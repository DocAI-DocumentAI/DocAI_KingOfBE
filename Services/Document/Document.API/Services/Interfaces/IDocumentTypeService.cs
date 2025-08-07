using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.Infrastructure.Paginate;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Document.API.Services.Interfaces
{
    /// <summary>
    /// Service interface for managing document types
    /// </summary>
    public interface IDocumentTypeService
    {
        /// <summary>
        /// Creates a new document type
        /// </summary>
        /// <param name="request">The document type creation request</param>
        /// <returns>The created document type response</returns>
        Task<DocumentTypeResponse> CreateDocumentTypeAsync(CreateDocumentTypeRequest request);

        /// <summary>
        /// Gets a document type by its ID
        /// </summary>
        /// <param name="documentTypeId">The ID of the document type</param>
        /// <returns>The document type response</returns>
        Task<DocumentTypeResponse> GetDocumentTypeByIdAsync(string documentTypeId);

        /// <summary>
        /// Gets all document types with pagination
        /// </summary>
        /// <param name="pageNumber">The page number</param>
        /// <param name="pageSize">The page size</param>
        /// <returns>Paginated list of document types</returns>
        Task<IPaginate<DocumentTypeResponse>> GetAllDocumentTypesAsync(int pageNumber, int pageSize);

        /// <summary>
        /// Updates an existing document type
        /// </summary>
        /// <param name="documentTypeId">The ID of the document type to update</param>
        /// <param name="request">The document type update request</param>
        /// <returns>The updated document type response</returns>
        Task<DocumentTypeResponse> UpdateDocumentTypeAsync(string documentTypeId, UpdateDocumentTypeRequest request);

        /// <summary>
        /// Deletes a document type
        /// </summary>
        /// <param name="documentTypeId">The ID of the document type to delete</param>
        /// <returns>Task representing the async operation</returns>
        Task DeleteDocumentTypeAsync(string documentTypeId);

        /// <summary>
        /// Gets all document types as a simple list (for dropdown/selection purposes)
        /// </summary>
        /// <returns>List of document types</returns>
        Task<List<DocumentTypeResponse>> GetDocumentTypesListAsync();
    }
}
