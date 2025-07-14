using AutoMapper;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Enums;
using Document.Domain.Model;
using Document.Domain.Models;
using Document.Infrastructure.Filter;
using Document.Infrastructure.Paginate;
using Document.Infrastructure.Repository.Interfaces;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2010.Word;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Shared.DTOs;
using Shared.Exceptions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Document.API.Services.Implements;

public class DocumentService : IDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<DocumentService> _logger;
    private readonly IAzureStorageService _storageService;
    private readonly IKernelMemory _memory;
    public DocumentService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<DocumentService> logger, IKernelMemory memory, IAzureStorageService storageService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _memory = memory;
        _storageService = storageService;
    }

    


    public async Task<DocumentDraftResponse> CreateDraftAsync(CreateDraftRequest request, string userId)
    {
        // Validations
        // BR-015 Supported file types are PDF (text-based) and DOCX.
        var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!PolicyConstant.SupportedFileTypes.Contains(fileExtension))
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.UnsupportedFileType);
        }

        // BR-016 Maximum file size is 5MB.
        if (request.File.Length > PolicyConstant.MaxFileSizeMB * 1024 * 1024)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.FileSizeExceeded, PolicyConstant.MaxFileSizeMB));
        }

        // BR-018 Every new document must be assigned to a single Department.
        if (string.IsNullOrEmpty(request.DepartmentId))
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.DepartmentNotAssigned);
        }

        // BR-021 'Effective From' date must be before 'Expiration Date'.
        if (request.EffectiveFrom.HasValue && request.EffectiveUntil.HasValue && request.EffectiveFrom.Value >= request.EffectiveUntil.Value)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.InvalidEffectiveDates);
        }

        //1. Check draft limit
        var draftCount = await _unitOfWork.GetRepository<DocumentVersion>()
            .CountAsync(predicate: v => v.CreatedBy == userId && v.Status == StatusEnum.Draft);
        if (draftCount >= PolicyConstant.MaxDraftsPerUser)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.MaxDraftsReached, PolicyConstant.MaxDraftsPerUser));
        }

        //2. Handle replacement logic if ReplacementDocumentId is provided
        DocumentFile? documentToReplace = null;
        if (!string.IsNullOrEmpty(request.ReplacementDocumentId))
        {
            documentToReplace = await _unitOfWork.GetRepository<DocumentFile>()
                .SingleOrDefaultAsync(
                    predicate: d => d.Id.ToString() == request.ReplacementDocumentId,
                    include: i => i.Include(d => d.DocumentVersions)
                ) ?? throw new ErrorException(StatusCodes.Status404NotFound, MessageConstant.DocumentNotFound);

            // BR-035: Only documents with status 'Approved' can be selected for replacement.
            var latestApprovedVersion = documentToReplace.DocumentVersions.OrderByDescending(v => v.CreatedTime).FirstOrDefault(v => v.Status == StatusEnum.Approved);
            if (latestApprovedVersion == null)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.CanOnlyReplaceApprovedDocument);
            }

            // BR-037: A document can only be in the process of being replaced by one new document at a time.
            if (documentToReplace.IsReplaced)
            {
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.DocumentAlreadyUnderReplacement);
            }

            // BR-038: Editors can only replace documents within their assigned Department.
            if (documentToReplace.DepartmentId != request.DepartmentId) // Assuming user's department is tied to the request's department
            {
                throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN, MessageConstant.UnauthorizedToReplaceDocumentInOtherDepartment);
            }

            // BR-036: The replacement file cannot be identical to the original (checked by hash, title, number).
            // This check will be done after file upload.
        }
        else
        {
            // If not a replacement, check for title duplication for new documents
            var existingDocument = await _unitOfWork.GetRepository<DocumentFile>()
                    .SingleOrDefaultAsync(predicate: d => d.Title == request.Title);
            if (existingDocument != null)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.DocumentTitleExists);
            }

            // Checking Version Name duplication for new documents
            var existingVersionName = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(predicate: v => v.VersionName == request.VersionName && v.DocumentFile.Title == request.Title);
            if (existingVersionName != null)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.DocumentVersionNameExists);
            }
        }

        // 4. Upload the file to Azure Storage and get the MD5 hash.
        var uploadResponse = await _storageService.UploadFileAsync(request.File, StorageFolderConstant.Drafts);
        var fileHash = uploadResponse.Md5Hash;

        // 5. Check for file duplication using the MD5 hash.
        var existingFile = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(predicate: v => v.FileHash == fileHash, include: i => i.Include(v => v.DocumentFile));

        if (existingFile != null)
        {
            await _storageService.DeleteFileAsync(uploadResponse.BlobName, StorageFolderConstant.Drafts);

            switch (existingFile.Status)
            {
                case StatusEnum.Pending:
                case StatusEnum.Approved:
                case StatusEnum.Archived:
                    throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, string.Format(MessageConstant.FileAlreadyExists, existingFile.DocumentFile.Title, existingFile.VersionName, existingFile.Status));

                case StatusEnum.Rejected:
                    if (existingFile.DocumentFile.OwnerId == userId)
                    {
                        throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.RejectedFileExists);
                    }
                    else
                    {
                        throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.AnotherUserRejectedFileExists);
                    }

                case StatusEnum.Draft:
                    if (existingFile.DocumentFile.OwnerId == userId)
                    {
                        throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.DraftFileExists);
                    }
                    break;
            }
        }

        //6. save the generel infomation of the file into the DocumentFile table
        var documentFile = new DocumentFile
        {
            Title = request.Title,
            Description = request.Description,
            DepartmentId = request.DepartmentId,
            OwnerId = userId,
            CreatedBy = userId,
            ReplacementId = request.ReplacementDocumentId,
            IsReplaced = !string.IsNullOrEmpty(request.ReplacementDocumentId)
        };

        var version = new DocumentVersion
        {
            DocumentFileId = documentFile.Id,
            DocumentFile = documentFile,
            Title = request.Title,
            VersionName = request.VersionName,
            Status = StatusEnum.Draft, // Use the Enum for status
            IsOfficial = false, // New drafts are not official
            Summary = request.Summary, // Placeholder for summary
            FileName = request.File.FileName,
            FileType = Path.GetExtension(request.File.FileName),
            FileSize = request.File.Length,
            FilePath = uploadResponse.BlobName,
            FileHash = fileHash,
            SignedBy = request.SignedBy,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveUntil = request.EffectiveUntil,
            CreatedBy = userId,
            LastSubmitted = DateTime.UtcNow,
            SubmittedBy = userId,
        };

        await ProcessTagsAsync(version, request.Tags, userId);

        // 6. Link entities using the correct navigation property name
        documentFile.DocumentVersions.Add(version);
        // 7. Save to database
        await _unitOfWork.GetRepository<DocumentFile>().InsertAsync(documentFile);
        await _unitOfWork.CommitAsync();

        if (documentToReplace != null)
        {
            documentToReplace.IsReplaced = true;
            documentToReplace.LastUpdatedBy = userId;
            documentToReplace.LastUpdatedTime = DateTime.UtcNow;
            _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentToReplace);
            await _unitOfWork.CommitAsync();
            _logger.LogInformation("Marked document {OriginalDocumentId} as replaced by new document {NewDocumentId}", documentToReplace.Id, documentFile.Id);
        }

        _logger.LogInformation("Successfully created draft document {DocumentId}", documentFile.Id);

        // 8. Use AutoMapper to map the result to the response DTO
        var response = _mapper.Map<DocumentDraftResponse>(documentFile);

        return response;
    }

    public async Task<DocumentDraftResponse> UpdateDraftAsync(string versionId, UpdateDocumentDraftRequest request, string userId)
    {
        // Validations
        if (request.File != null)
        {
            // BR-015 Supported file types are PDF (text-based) and DOCX.
            var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            if (!PolicyConstant.SupportedFileTypes.Contains(fileExtension))
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.UnsupportedFileType);
            }

            // BR-016 Maximum file size is 5MB.
            if (request.File.Length > PolicyConstant.MaxFileSizeMB * 1024 * 1024)
            {
                throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, string.Format(MessageConstant.FileSizeExceeded, PolicyConstant.MaxFileSizeMB));
            }
        }

        // BR-021 'Effective From' date must be before 'Expiration Date'.
        if (request.EffectiveFrom.HasValue && request.EffectiveUntil.HasValue && request.EffectiveFrom.Value >= request.EffectiveUntil.Value)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, MessageConstant.InvalidEffectiveDates);
        }

        //1. Retrive draft to update
        var versionToUpdate = await _unitOfWork.GetRepository<DocumentVersion>()
        .SingleOrDefaultAsync(
            predicate: v => v.Id == versionId,
            include: p => p.Include(v => v.DocumentFile)) ?? throw new ErrorException(StatusCodes.Status404NotFound, MessageConstant.DocumentVersionNotFound);

        var documentToUpdate = versionToUpdate.DocumentFile;

        //2. Editor must be the owner. 
        if (documentToUpdate.OwnerId != userId)
        {
            throw new ErrorException(StatusCodes.Status403Forbidden, MessageConstant.UnauthorizedToEdit);
        }

        //3. Status must be Draft or Rejected. 
        if (versionToUpdate.Status != StatusEnum.Draft && versionToUpdate.Status != StatusEnum.Rejected)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, string.Format(MessageConstant.CannotEditWithStatus, versionToUpdate.Status));
        }

        //4. Handle file replacement if a new file is provided.
        if (request.File != null)
        {
            _logger.LogInformation("Replacing file for document version {VersionId}.", versionId);

            // Upload the new file to Azure Storage and get the MD5 hash.
            var uploadResponse = await _storageService.UploadFileAsync(request.File, StorageFolderConstant.Drafts);
            var fileHash = uploadResponse.Md5Hash;

            // Check for file duplication using the MD5 hash.
            var existingFile = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(predicate: v => v.FileHash == fileHash && v.Id != versionId && v.Status != StatusEnum.Rejected, include: i => i.Include(v => v.DocumentFile));

            if (existingFile != null)
            {
                // If a duplicate is found, delete the file that was just uploaded.
                await _storageService.DeleteFileAsync(uploadResponse.BlobName, StorageFolderConstant.Drafts);

                _logger.LogWarning("Duplicate file detected during update. Hash: {FileHash}. Existing document: {DocumentTitle}, Version: {VersionName}, Status: {Status}",
                    fileHash, existingFile.DocumentFile.Title, existingFile.VersionName, existingFile.Status);
                throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT,
                    string.Format(MessageConstant.FileAlreadyExists, existingFile.DocumentFile.Title, existingFile.VersionName, existingFile.Status));
            }

            // 1. Delete the old file from Azure Storage.
            await _storageService.DeleteFileAsync(versionToUpdate.FileName, StorageFolderConstant.Drafts);

            // 2. Update version properties for the new file.
            versionToUpdate.FilePath = uploadResponse.BlobName;
            versionToUpdate.FileName = request.File.FileName;
            versionToUpdate.FileType = Path.GetExtension(request.File.FileName);
            versionToUpdate.FileSize = request.File.Length;
            versionToUpdate.FileHash = fileHash;
        }

        // Apply metadata updates from the request DTO.
        _mapper.Map(request, documentToUpdate);
        _mapper.Map(request, versionToUpdate);

        await ProcessTagsAsync(versionToUpdate, request.Tags, userId);

        documentToUpdate.LastUpdatedBy = userId;
        documentToUpdate.LastUpdatedTime = DateTime.UtcNow;

        if (versionToUpdate.Status == StatusEnum.Rejected)
        {
            versionToUpdate.Status = StatusEnum.Draft;
        }

        // Save changes to the database.
        _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(versionToUpdate);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Successfully updated document version {VersionId}", versionId);

        return _mapper.Map<DocumentDraftResponse>(documentToUpdate);
    }


    public async Task<AnalyzeDocumentResponse> AnalyzeDocumentAsync(IFormFile file)
    {
        _logger.LogInformation("Starting single-prompt AI analysis for file: {FileName}", file.FileName);

        var response = new AnalyzeDocumentResponse
        {
            Summary = "AI analysis could not be completed.",
            Tags = new List<string>()
        };
        string tempFilePath = null;
        string tempDocId = null;
        try
        {
            tempDocId = $"temp-analysis-{Guid.NewGuid()}";
            tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));
            await using (var fs = new FileStream(tempFilePath, FileMode.Create)) { await file.CopyToAsync(fs); }

            // 1. Build the temporary Kernel Memory instance (same as before)
            await _memory.ImportDocumentAsync(tempFilePath, documentId: tempDocId);

            // 2. Engineer a single, comprehensive prompt asking for a JSON response
            const string comprehensivePrompt = @"
                Analyze the document and extract the following metadata.
                Response language based on the document language.
                Respond with ONLY a single, valid JSON object and nothing else.
                The JSON object must have these keys: ""title"", ""summary"", ""tags"", ""effectiveFrom"", ""effectiveUntil"", ""signedBy"".
                - 'summary' should be a concise 3-4 sentence overview.
                - 'tags' should be a JSON array of up to 5 relevant string keywords.
                - 'effectiveFrom' and 'effectiveUntil' must be in 'yyyy-MM-dd' format if found.
                - If a value for any key is not found in the document, use null as the value.
            ";

            // 3. Make a single call to the AI model
            var filter = new MemoryFilter().ByDocument(tempDocId);

            MemoryAnswer answer = null;
            const int maxRetries = 3;
            const int delayBetweenRetriesMs = 1500;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                answer = await _memory.AskAsync(comprehensivePrompt, filter: filter);

                if (answer != null && answer.RelevantSources.Any() && !answer.Result.Contains("INFO NOT FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Successfully received valid AI response on attempt {AttemptNumber}.", attempt);
                    break;
                }

                _logger.LogWarning("AI analysis attempt {AttemptNumber} of {MaxRetries} failed or returned no relevant sources. Retrying...", attempt, maxRetries);

                if (attempt < maxRetries)
                {
                    await Task.Delay(delayBetweenRetriesMs);
                }
            }

            // 4. Parse the structured JSON response from the AI
            if (!answer.Result.Contains("INFO NOT FOUND", StringComparison.OrdinalIgnoreCase))
            {
                ParseAiJsonResponse(answer.Result, response);
                _logger.LogInformation("Successfully parsed AI JSON response for file: {FileName}", file.FileName);

                // BR-077: Summaries should be under 1000 words.
                if (!string.IsNullOrEmpty(response.Summary))
                {
                    var words = response.Summary.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length > PolicyConstant.MaxSummaryLength)
                    {
                        response.Summary = string.Join(" ", words.Take(PolicyConstant.MaxSummaryLength)) + "...";
                        _logger.LogWarning("AI-generated summary for file {FileName} exceeded {MaxLength} words and was truncated.", file.FileName, PolicyConstant.MaxSummaryLength);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during single-prompt AI analysis for file: {FileName}", file.FileName);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
            // Clean up the temporary document from Kernel Memory
            if(tempDocId != null)
            {
                await _memory.DeleteDocumentAsync(tempDocId);
            }
        }

        return response;
    }

    private void ParseAiJsonResponse(string jsonResponse, AnalyzeDocumentResponse response)
    {
        try
        {
            // The AI might sometimes include markdown ```json ... ``` tags, so we clean it.
            var cleanJson = jsonResponse.Trim().Trim('`').Replace("json", "").Trim();

            using var jsonDoc = JsonDocument.Parse(cleanJson);
            var root = jsonDoc.RootElement;

            // Safely get each property from the parsed JSON
            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                response.Title = title.GetString();

            if (root.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.String)
                response.Summary = summary.GetString();

            if (root.TryGetProperty("signedBy", out var signedBy) && signedBy.ValueKind == JsonValueKind.String)
                response.SignedBy = signedBy.GetString();

            if (root.TryGetProperty("effectiveFrom", out var effectiveFrom) && effectiveFrom.ValueKind == JsonValueKind.String)
                if (DateTime.TryParse(effectiveFrom.GetString(), out var date)) response.EffectiveFrom = date;

            if (root.TryGetProperty("effectiveUntil", out var effectiveUntil) && effectiveUntil.ValueKind == JsonValueKind.String)
                if (DateTime.TryParse(effectiveUntil.GetString(), out var date)) response.EffectiveUntil = date;

            if (root.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
            {
                response.Tags = tags.EnumerateArray()
                                    .Select(tag => tag.GetString())
                                    .Where(t => !string.IsNullOrEmpty(t))
                                    .ToList();
            }
        }
        catch (JsonException jex)
        {
            _logger.LogWarning(jex, "Failed to parse JSON response from AI. Response was: {AiResponse}", jsonResponse);
            // We do not throw; the method will return the default/partially filled response object.
        }
    }

    public async Task DeleteDraftAsync(string documentId, string versionId, string userId)
    {
        _logger.LogInformation("Attempting to delete document {DocumentId} by user {UserId}", documentId, userId);

        // 1. Retrieve the document, ensuring its versions are included for status checking.
        var documentToDelete = await _unitOfWork.GetRepository<DocumentFile>()
            .SingleOrDefaultAsync(
                predicate: d => d.Id == documentId,
                include: q => q.Include(d => d.DocumentVersions)
            ) ?? throw new ErrorException(StatusCodes.Status404NotFound, MessageConstant.DocumentNotFound);

        _logger.LogInformation("Document found: {Title}", documentToDelete.Title);

        // 2. Enforce Business Rules from SRS
        // BR-116: Check if the current user is the owner.
        if (documentToDelete.OwnerId != userId)
        {
            _logger.LogWarning("User {UserId} attempted to delete a document they do not own.", userId);
            throw new ErrorException(StatusCodes.Status403Forbidden, MessageConstant.UnauthorizedToDelete);
        }

        _logger.LogInformation("User {UserId} is the owner of the document", userId);

        // A draft document should only have one version. We get that version to check its status.
        var versionToDelete = documentToDelete.DocumentVersions.FirstOrDefault(v => v.Id == versionId);

        // BR-117: Check if the document's status is "Draft".
        if (versionToDelete == null || versionToDelete.Status != StatusEnum.Draft)
        {
            var currentStatus = versionToDelete?.Status.ToString() ?? "Unknown";
            var message = string.Format(MessageConstant.CanOnlyDeleteDrafts, currentStatus);
            _logger.LogWarning("Attempted to delete a document with status '{Status}', not 'Draft'.", currentStatus);
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, message);
        }

        _logger.LogInformation("Version to delete: {VersionName}, Status: {Status}", versionToDelete.VersionName, versionToDelete.Status);

        // 3. Delete the physical file from Azure Storage.
        _logger.LogInformation("Deleting file from Azure Storage: {FileName}", versionToDelete.FileName);
        await _storageService.DeleteFileAsync(versionToDelete.FileName, StorageFolderConstant.Drafts);
        _logger.LogInformation("Deleted file from Azure Storage at path: {FilePath}", versionToDelete.FilePath);

        // 4. Delete the DocumentFile record from the database.
        // Due to cascade delete settings, this will also remove the associated DocumentVersion(s) and VersionTag(s).
        _logger.LogInformation("Deleting document from database: {DocumentId}", documentId);
        _unitOfWork.GetRepository<DocumentFile>().DeleteAsync(documentToDelete);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("User {UserId} successfully deleted draft document {DocumentId}.", userId, documentId);

        // TODO: As per SRS 3.4.3, this action should be recorded in the system audit log.
    }

    public async Task<IPaginate<DocumentDraftResponse>> GetDraftsAsync(string userId, int pageNumber, int pageSize)
    {
        var drafts = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            filter: null,
            selector: d => new DocumentDraftResponse
            {
                DocumentId = d.DocumentFile.Id.ToString(),
                VersionId = d.Id,
                VersionName = d.VersionName,
                Title = d.DocumentFile.Title,
                Summary = d.Summary,
                FileName = d.FileName,
                FileType = d.FileType,
                FileSize = d.FileSize,
                FilePath = d.FilePath,
                Status = d.Status.ToString(),
                DepartmentId = d.DocumentFile.DepartmentId,
                OwnerId = d.DocumentFile.OwnerId,
                CreatedTime = d.DocumentFile.CreatedTime
            },
            predicate: v => v.DocumentFile.OwnerId == userId && v.Status == StatusEnum.Draft,
            include: i => i.Include(v => v.DocumentFile).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.CreatedTime),
            page: pageNumber,
            size: pageSize
        );

        return _mapper.Map<IPaginate<DocumentDraftResponse>>(drafts);
    }

    public async Task<DocumentDraftResponse> GetDraftByIdAsync(string versionId, string userId)
    {
        var draft = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.Id == versionId && v.DocumentFile.OwnerId == userId && v.Status == StatusEnum.Draft,
            include: i => i.Include(v => v.DocumentFile).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        if (draft == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, MessageConstant.DraftDocumentNotFound);
        }

        return _mapper.Map<DocumentDraftResponse>(draft);
    }

    public async Task<IPaginate<DocumentDraftResponse>> GetRejectDocumentsAsync(string userId, int pageNumber, int pageSize)
    {
        var rejectedDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            filter: null,
            selector : d => new DocumentDraftResponse
            {
                DocumentId = d.DocumentFile.Id.ToString(),
                VersionId = d.Id,
                VersionName = d.VersionName,
                Title = d.DocumentFile.Title,
                Summary = d.Summary,
                FileName = d.FileName,
                FileType = d.FileType,
                FileSize = d.FileSize,
                FilePath = d.FilePath,
                Status = d.Status.ToString(),
                DepartmentId = d.DocumentFile.DepartmentId,
                OwnerId = d.DocumentFile.OwnerId,
                CreatedTime = d.DocumentFile.CreatedTime
            },
            predicate: v => v.DocumentFile.OwnerId == userId && v.Status == StatusEnum.Rejected,
            include: i => i.Include(v => v.DocumentFile).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.LastUpdatedTime),
            page: pageNumber,
            size: pageSize
        );

        return _mapper.Map<IPaginate<DocumentDraftResponse>>(rejectedDocuments);
    }

    public async Task<DocumentDraftResponse> GetRejectedById(string versionId, string userId)
    {
        var rejectedDocument = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.Id == versionId && v.DocumentFile.OwnerId == userId && v.Status == StatusEnum.Rejected,
            include: i => i.Include(v => v.DocumentFile).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        if (rejectedDocument == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, MessageConstant.RejectedDocumentNotFound);
        }

        return _mapper.Map<DocumentDraftResponse>(rejectedDocument);
    }

    public async Task<DocumentDraftResponse> GetOfficialDocumentAsync(string documentFileId)
    {
        var officialDocument = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: v => v.DocumentFileId == documentFileId && v.IsOfficial,
            include: i => i.Include(v => v.DocumentFile).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        if (officialDocument == null)
        {
            throw new ErrorException(StatusCodes.Status404NotFound, MessageConstant.OfficialDocumentNotFoundForId);
        }

        return _mapper.Map<DocumentDraftResponse>(officialDocument);
    }

    public async Task<IPaginate<DocumentDraftResponse>> GetAllOfficialDocumentsAsync(int pageNumber, int pageSize)
    {
        var officialDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            filter: null,
            selector: d => new DocumentDraftResponse
            {
                DocumentId = d.DocumentFile.Id.ToString(),
                VersionId = d.Id,
                VersionName = d.VersionName,
                Title = d.DocumentFile.Title,
                Summary = d.Summary,
                FileName = d.FileName,
                FileType = d.FileType,
                FileSize = d.FileSize,
                FilePath = d.FilePath,
                Status = d.Status.ToString(),
                DepartmentId = d.DocumentFile.DepartmentId,
                OwnerId = d.DocumentFile.OwnerId,
                CreatedTime = d.DocumentFile.CreatedTime
            },
            predicate: v => v.IsOfficial,
            include: i => i.Include(v => v.DocumentFile).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.CreatedTime),
            page: pageNumber,
            size: pageSize
        );

        return _mapper.Map<IPaginate<DocumentDraftResponse>>(officialDocuments);
    }

    public async Task<IPaginate<DocumentDraftResponse>> GetMyDocumentsAsync(string userId, MyDocumentsFilter filter, int pageNumber, int pageSize)
    {

        var myDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
            selector: dv => _mapper.Map<DocumentDraftResponse>(dv),
            filter: filter,
            predicate: d => d.DocumentFile.OwnerId == userId,
            include: i => i.Include(v => v.DocumentFile).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag),
            orderBy: q => q.OrderByDescending(v => v.DocumentFile.CreatedTime),
            page: pageNumber,
            size: pageSize
        );

        return myDocuments;
    }

    public async Task<DocumentVersionResponse> GetDocumentVersionAsync(string documentId, string versionId)
    {
        var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
            predicate: dv => dv.DocumentFileId == documentId && dv.Id.ToString() == versionId,
            include: i => i.Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        return _mapper.Map<DocumentVersionResponse>(documentVersion);
    }

    public async Task<List<DocumentVersionResponse>> GetDocumentVersionsAsync(string documentId)
    {
        var documentVersions = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
            predicate: dv => dv.DocumentFileId == documentId,
            include: i => i.Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
        );

        return _mapper.Map<List<DocumentVersionResponse>>(documentVersions);
    }

    public async Task<DocumentDraftResponse> CreateNewVersionAsync(string documentId, CreateDraftRequest request, string userId)
    {
        var documentToUpdate = await _unitOfWork.GetRepository<DocumentFile>().SingleOrDefaultAsync(
            predicate: d => d.Id.ToString() == documentId,
            include: i => i.Include(d => d.DocumentVersions)
        ) ?? throw new ErrorException(StatusCodes.Status404NotFound, MessageConstant.DocumentNotFound);

        // BR-037: A document can only be in the process of being replaced by one new document at a time.
        var pendingVersion = documentToUpdate.DocumentVersions.FirstOrDefault(v => v.Status == StatusEnum.Pending);
        if (pendingVersion != null)
        {
            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, MessageConstant.DocumentAlreadyUnderReplacement);
        }

        // BR-038: Editors can only replace documents within their assigned Department.
        // This check would typically involve retrieving the user's department(s) and comparing with documentToUpdate.DepartmentId.
        // Assuming department-based authorization is handled at a higher layer (e.g., controller/middleware) or user context needs to be enriched.

        if (documentToUpdate.OwnerId != userId)
        {
            throw new ErrorException(StatusCodes.Status403Forbidden, MessageConstant.UnauthorizedToCreateNewVersion);
        }

        var latestVersion = documentToUpdate.DocumentVersions.OrderByDescending(v => v.CreatedTime).FirstOrDefault();

        if (latestVersion.Status != StatusEnum.Approved)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, MessageConstant.CanOnlyCreateNewVersionOfApproved);
        }

        var uploadResponse = await _storageService.UploadFileAsync(request.File, StorageFolderConstant.Drafts);
        var fileHash = uploadResponse.Md5Hash;

        var existingFile = await _unitOfWork.GetRepository<DocumentVersion>()
            .SingleOrDefaultAsync(predicate: v => v.FileHash == fileHash, include: i => i.Include(v => v.DocumentFile));

        if (existingFile != null)
        {
            await _storageService.DeleteFileAsync(uploadResponse.BlobName, StorageFolderConstant.Drafts);

            throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, $"This file already exists in the system as '{existingFile.DocumentFile.Title}' (Version: {existingFile.VersionName}, Status: {existingFile.Status}).");
        }

        var newVersion = new DocumentVersion
        {
            DocumentFileId = documentToUpdate.Id,
            DocumentFile = documentToUpdate,
            Title = request.Title,
            VersionName = request.VersionName,
            Status = StatusEnum.Draft,
            IsOfficial = false,
            Summary = request.Summary,
            FileName = request.File.FileName,
            FileType = Path.GetExtension(request.File.FileName),
            FileSize = request.File.Length,
            FilePath = uploadResponse.BlobName,
            FileHash = fileHash,
            SignedBy = request.SignedBy,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveUntil = request.EffectiveUntil,
            CreatedBy = userId,
        };

        await ProcessTagsAsync(newVersion, request.Tags, userId);

        documentToUpdate.DocumentVersions.Add(newVersion);
        await _unitOfWork.GetRepository<DocumentFile>().UpdateAsync(documentToUpdate);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Successfully created new version for document {DocumentId}", documentToUpdate.Id);

        return _mapper.Map<DocumentDraftResponse>(newVersion);
    }

    private async Task ProcessTagsAsync(DocumentVersion version, IEnumerable<string> tagNames, string userId)
    {
        if (tagNames != null && tagNames.Count() == 1 && tagNames.First().Contains(','))
        {
            tagNames = tagNames.First().Split(',').Select(t => t.Trim()).ToList();
        }

        // Clear existing tags for the version
        var existingDocumentTags = await _unitOfWork.GetRepository<DocumentTag>()
            .GetListWithTrackingAsync(predicate: dt => dt.DocumentVersionId == version.Id);
        if (existingDocumentTags.Any())
        {
            _unitOfWork.GetRepository<DocumentTag>().DeleteRangeAsync(existingDocumentTags);
        }

        if (tagNames == null || !tagNames.Any())
        {
            return;
        }

        var distinctTagNames = tagNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _logger.LogInformation("Processing tags: {Tags}", JsonSerializer.Serialize(tagNames));
        _logger.LogInformation("Distinct tags: {Tags}", JsonSerializer.Serialize(distinctTagNames));
        
        // Find which tags already exist in the database
        var existingTags = await _unitOfWork.GetRepository<Tag>()
            .GetListWithTrackingAsync(predicate: t => distinctTagNames.Contains(t.Name));

        var existingTagNames = existingTags.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation("Existing tags: {Tags}", JsonSerializer.Serialize(existingTags.Select(t => t.Name)));

        // Create a list of the new tags that need to be inserted
        var newTagsToInsert = new List<Tag>();
        foreach (var tagName in distinctTagNames)
        {
            if (!existingTagNames.Contains(tagName))
            {
                newTagsToInsert.Add(new Tag { Name = tagName.ToLowerInvariant(), CreatedBy = userId });
            }
        }

        // Add the new tags to the change tracker
        if (newTagsToInsert.Any())
        {
            await _unitOfWork.GetRepository<Tag>().InsertRangeAsync(newTagsToInsert);
        }

        // Combine the existing tags and the new tags
        var allTagsForDocument = existingTags.Concat(newTagsToInsert).ToList();

        // Create the links between the document version and the tags
        foreach (var tag in allTagsForDocument)
        {
            version.DocumentTags.Add(new DocumentTag { Tag = tag });
        }
    }
    

    public async Task<IPaginate<DocumentResponse>> SemanticSearch(SemanticSearchRequest request, DocumentFilter filter, string userId, int pageNumber, int pageSize)
    {
        var memoryFilter = new MemoryFilter();

        if (filter.IsPublic.HasValue)
        {
            memoryFilter.Add("isPublic", filter.IsPublic.Value.ToString().ToLower());
        }
        if (filter.DepartmentId.HasValue)
        {
            memoryFilter.Add("departmentId", filter.DepartmentId.Value.ToString());
        }

        var searchResult = await _memory.SearchAsync(request.Query, limit: pageSize, filter: memoryFilter);

        var documentResponses = new List<DocumentResponse>();

        foreach (var item in searchResult.Results)
        {
            foreach (var partition in item.Partitions)
            {
                // Assuming DocumentId is stored as a tag in Kernel Memory
                if (partition.Tags.TryGetValue("documentId", out var documentIds) && documentIds.Any())
                {
                    var documentId = documentIds.First();
                    var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>().SingleOrDefaultAsync(
                        predicate: dv => dv.DocumentFile.Id.ToString() == documentId && dv.Status == StatusEnum.Approved && filter.ToExpression().Compile().Invoke(dv),
                        include: i => i.Include(v => v.DocumentFile).Include(v => v.DocumentTags).ThenInclude(dt => dt.Tag)
                    );

                    if (documentVersion != null)
                    {
                        documentResponses.Add(_mapper.Map<DocumentResponse>(documentVersion));
                    }
                }
            }
        }

        // Manually create IPaginate from the list
        var totalCount = searchResult.Results.Count; // This is not the true total count, but KM doesn't provide it directly
        var paginateResult = new Paginate<DocumentResponse>(documentResponses, pageNumber, pageSize, totalCount);

        return paginateResult;
    }
}