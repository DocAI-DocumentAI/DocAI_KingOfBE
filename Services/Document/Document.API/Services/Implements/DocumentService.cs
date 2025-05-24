using AutoMapper;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Model;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using Shared.Exceptions;

namespace Document.API.Services.Implements;

public class DocumentService : IDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<DocumentService> _logger;
    private readonly string _storagePath = "UploadedDocuments";
    private readonly FileUtil _fileUtils = new FileUtil();
    public DocumentService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<DocumentService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }
    /// <summary>
    /// Uploads a document file and saves its information to the database, 
    /// the general information will be saved into DocumentFile table, 
    /// the file version and path will be saved into DocumentVersion table, 
    /// the text content will be extracted and saved into DocumentChunk table.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="ErrorException"></exception>
    public async Task UploadDocumentAsync(UploadDocumentRequest request)
    {
        try
        {
            //1. saving file to local storage
            Directory.CreateDirectory(_storagePath);
            //var filePath = Path.Combine(_storagePath, request.File.FileName);
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), request.File.FileName);
            // Get file extension (.PDF, .DOCX)
            var fileExt = Path.GetExtension(request.File.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }
            _logger.LogInformation("File saved to ${FilePath}", filePath);
            //2. save the generel infomation of the file into the DocumentFile table

            // Checking titile duplication
            var existingDocument = await _unitOfWork.GetRepository<DocumentFile>()
                .SingleOrDefaultAsync(predicate: d => d.Title == request.Title);
            if(existingDocument != null)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, "Document title already exists");
            }

            // Creating new document object
            var documentFile = new DocumentFile
            {
                Title = request.Title,
                DocumentName = request.File.FileName,
                Description = request.Description,
                StoragePath = filePath,
                Status ="Uploaded", // temp
                CreatedBy = "system", // temp
                CreatedTime = DateTime.UtcNow,
            };

            await _unitOfWork.GetRepository<DocumentFile>().InsertAsync(documentFile);
            _logger.LogInformation("Document file information saved to database");


            //3. save the file version and path into the Document version table
            var documentVersion = new DocumentVersion
            {
                DocumentId = documentFile.Id,
                Version = "1",
                FilePath = filePath,
                FileName = request.File.FileName,
                FileType = fileExt,
                FileSize = request.File.Length,
                CreatedBy = "system",
                CreatedTime = DateTime.UtcNow,
            };
            await _unitOfWork.GetRepository<DocumentVersion>().InsertAsync(documentVersion);
            _logger.LogInformation("Document version information saved to database");

            //4. extract the text from the file 
            _logger.LogInformation("Extracting text from file: {FilePath}", filePath);
            var text = _fileUtils.EtractText(filePath);

            //5. Split them into chunks, for search and embeddng purposr
            _logger.LogInformation("Splitting text into chunks");
            // min text length = 300, max text length = 500, overlap = 10%
            var chunks = _fileUtils.SplitTextIntoChunks(text, 300, 500, 0.1);

            //6. save the chunks into the DocumentChunk table
            int order = 0;
            foreach (var chunk in chunks)
            {
                var documentChunk = new DocumentChunk
                {
                    DocumentId = documentFile.Id,
                    ChunkOrder = order++,
                    Text = chunk
                };
                _logger.LogInformation("Saving chunk {Order} to database", order);
                await _unitOfWork.GetRepository<DocumentChunk>().InsertAsync(documentChunk);
            }

            // Save change
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Done. All changes saved to database");
        }
        catch (ErrorException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document:", ex.Message);
            throw new ErrorException(StatusCodes.Status500InternalServerError, ErrorCode.INTERNAL_SERVER_ERROR, "Error while uploading document, try again later");
        }
    }
    /// <summary>
    /// Gets a document by ID with its information and text content
    /// </summary>
    /// <param name="documentId">The ID of the document to retrieve</param>
    /// <returns>Document information and content</returns>
    public async Task<DocumentResponse> GetDocumentByIdAsync(string documentId)
    {
        try
        {
            _logger.LogInformation("Retrieving document with ID: {DocumentId}", documentId);

            // 1. Get the document file information
            var documentFile = await _unitOfWork.GetRepository<DocumentFile>()
                .SingleOrDefaultAsync(predicate: d => d.Id == documentId) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Document not found");

            // 2. Get the latest document version
            var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(
                    predicate: v => v.DocumentId == documentId,
                    orderBy: versions => versions.OrderByDescending(v => v.CreatedTime));

            // 3. Get all document chunks and combine them
            var documentChunks = await _unitOfWork.GetRepository<DocumentChunk>()
                .GetListAsync(
                    predicate: c => c.DocumentId == documentId,
                    orderBy: chunks => chunks.OrderBy(c => c.ChunkOrder));

            // 4. Combine all chunk texts into a single document text
            string fullText = string.Join("\n", documentChunks.Select(c => c.Text));

            //// 5. Map to response object
            //var response = new DocumentResponse
            //{
            //    Id = documentFile.Id,
            //    Title = documentFile.Title,
            //    DocumentName = documentFile.DocumentName,
            //    Description = documentFile.Description,
            //    Status = documentFile.Status,
            //    CreatedBy = documentFile.CreatedBy,
            //    CreatedTime = documentFile.CreatedTime,
            //    FilePath = documentVersion?.FilePath,
            //    FileType = documentVersion?.FileType,
            //    FileSize = documentVersion?.FileSize ?? 0,
            //    Version = documentVersion?.Version,
            //    Text = fullText
            //};

            var response = _mapper.Map<DocumentResponse>(documentFile);

            if (documentVersion != null)
            {
                response.FilePath = documentVersion.FilePath;
                response.FileType = documentVersion.FileType;
                response.FileSize = documentVersion.FileSize;
                response.Version = documentVersion.Version;
            }

            response.Text = fullText;

            _logger.LogInformation("Successfully retrieved document with ID: {DocumentId}", documentId);
            return response;
        }
        catch (ErrorException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document with ID: {DocumentId}", documentId);
            throw new ErrorException(StatusCodes.Status500InternalServerError, ErrorCode.INTERNAL_SERVER_ERROR,
                "Error while retrieving document, please try again later");
        }
    }

    /// <summary>
    /// Updates the metadata of a document in the database.
    /// Interacts with only DocumentFile table.
    /// </summary>
    /// <param name="documentId"></param>
    /// <param name="document"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<DocumentFileResponse> UpdateMetaDataDocumentAsync(string documentId, UpdateMetaDataReqest request)
    {
        try
        {
            // 1. Retrive document file information
            var documentFile = await _unitOfWork.GetRepository<DocumentFile>()
                .SingleOrDefaultAsync(predicate: d => d.Id == documentId) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Document not found");

            // 2. Check for title duplication
            var existingDocument = await _unitOfWork.GetRepository<DocumentFile>()
                .SingleOrDefaultAsync(predicate: d => d.Title == request.Title && d.Id != documentId);

            // 3. Map the updated values
            _mapper.Map(request, documentFile);
            // 4. Update last modified information
            documentFile.LastUpdatedBy = "system"; // TEMP
            documentFile.LastUpdatedTime = DateTime.UtcNow;
            // 5. Update the document file in the database
            _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentFile);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Document metadata updated successfully for ID: {DocumentId}", documentId);

            // 6. Return updated document
            return _mapper.Map<DocumentFileResponse>(documentFile);
        }
        catch (ErrorException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document with ID: {DocumentId}", documentId);
            throw new ErrorException(StatusCodes.Status500InternalServerError, ErrorCode.INTERNAL_SERVER_ERROR,
                "Error while updating document, please try again later");
        }
    }
    /// <summary>
    /// Deletes a document and all its related data (versions, chunks, tags) from the database.
    /// Also deletes the physical files from storage.
    /// </summary>
    /// <param name="documentId">The ID of the document to delete</param>
    /// <exception cref="ErrorException">Thrown when document is not found or other errors occur</exception>
    public async Task DeleteDocumentAsync(string documentId)
    {
        try
        {
            _logger.LogInformation("Deleting document with ID: {DocumentId}", documentId);

            // 1. Get the document file information
            var documentFile = await _unitOfWork.GetRepository<DocumentFile>()
                .SingleOrDefaultAsync(predicate: d => d.Id == documentId) ?? throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND, "Document not found");

            // 2. Get all document versions to delete their physical files
            var documentVersions = await _unitOfWork.GetRepository<DocumentVersion>()
                .GetListAsync(predicate: v => v.DocumentId == documentId);

            // 3. Delete the physical files from storage
            foreach (var version in documentVersions)
            {
                if (File.Exists(version.FilePath))
                {
                    try
                    {
                        File.Delete(version.FilePath);
                        _logger.LogInformation("Deleted physical file: {FilePath}", version.FilePath);
                    }
                    catch (Exception ex)
                    {
                        // Log but continue with deletion
                        _logger.LogWarning(ex, "Failed to delete physical file: {FilePath}", version.FilePath);
                    }
                }
            }

            // 4. Delete the document fil
            _unitOfWork.GetRepository<DocumentFile>().DeleteAsync(documentFile);
            _logger.LogInformation("Deleting document file and related data through cascade delete");

            // 5. Commit the changes
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Successfully deleted document with ID: {DocumentId}", documentId);
        }
        catch (ErrorException)
        {
            // Let specific error exceptions propagate
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document with ID: {DocumentId}", documentId);
            throw new ErrorException(StatusCodes.Status500InternalServerError, ErrorCode.INTERNAL_SERVER_ERROR,
                "Error while deleting document, please try again later");
        }
    }


}