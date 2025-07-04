using AutoMapper;
using Document.API.Constants;
using Document.API.Payload.Request;
using Document.API.Payload.Response;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Enums;
using Document.Domain.Model;
using Document.Domain.Models;
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
        //1. Check draft limit
        var draftCount = await _unitOfWork.GetRepository<DocumentVersion>()
            .CountAsync(predicate: v => v.CreatedBy == userId && v.Status == StatusEnum.Draft);
        if (draftCount >= PolicyConstant.MaxDraftsPerUser)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, $"You have reached the maximum limit of {PolicyConstant.MaxDraftsPerUser} draft documents.");
        }

        //2. Checking title duplication
        var existingDocument = await _unitOfWork.GetRepository<DocumentFile>()
                .SingleOrDefaultAsync(predicate: d => d.Title == request.Title);
        if (existingDocument != null)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, "Document title already exists");
        }

        //3. Checking Version Name duplication
        var existingVersionName = await _unitOfWork.GetRepository<DocumentVersion>()
                .SingleOrDefaultAsync(predicate: v => v.VersionName == request.VersionName && v.DocumentFile.Title == request.Title);
        if (existingVersionName != null)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, ErrorCode.BADREQUEST, "Document version name already exists for this title");
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
                    throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, $"This file already exists in the system as '{existingFile.DocumentFile.Title}' (Version: {existingFile.VersionName}, Status: {existingFile.Status}).");

                case StatusEnum.Rejected:
                    if (existingFile.DocumentFile.OwnerId == userId)
                    {
                        throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, "You have a rejected document with the same file. Please resubmit or delete the existing one.");
                    }
                    else
                    {
                        throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, "Another user has a rejected document with the same file.");
                    }

                case StatusEnum.Draft:
                    if (existingFile.DocumentFile.OwnerId == userId)
                    {
                        throw new ErrorException(StatusCodes.Status409Conflict, ErrorCode.CONFLICT, "You already have a draft with the same file.");
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
        };

        if (request.Tags != null && request.Tags.Any())
        {
            version.DocumentTags = new List<DocumentTag>();

            var tagRepository = _unitOfWork.GetRepository<Tag>();

            foreach (var tagName in request.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var normalizedTag = tagName.ToLowerInvariant();

                // Tìm tag theo tên
                var existingTag = await tagRepository.SingleOrDefaultAsync(predicate: t => t.Name == normalizedTag);

                // Nếu chưa tồn tại, insert vào DB để sinh Id
                if (existingTag == null)
                {
                    existingTag = new Tag
                    {
                        Name = normalizedTag,
                        CreatedBy = userId
                    };

                    await tagRepository.InsertAsync(existingTag);
                }

                version.DocumentTags.Add(new DocumentTag { Tag = existingTag });
            }
        }

        // 6. Link entities using the correct navigation property name
        documentFile.DocumentVersions.Add(version);
        // 7. Save to database
        await _unitOfWork.GetRepository<DocumentFile>().InsertAsync(documentFile);
        await _unitOfWork.CommitAsync();

        _logger.LogInformation("Successfully created draft document {DocumentId}", documentFile.Id);

        // 8. Use AutoMapper to map the result to the response DTO
        var response = _mapper.Map<DocumentDraftResponse>(documentFile);

        return response;
    }

    public async Task<DocumentDraftResponse> UpdateDraftAsync(string versionId, UpdateDocumentDraftRequest request, string userId)
    {
        //1. Retrive draft to update
        var versionToUpdate = await _unitOfWork.GetRepository<DocumentVersion>()
        .SingleOrDefaultAsync(
            predicate: v => v.Id == versionId,
            include: p => p.Include(v => v.DocumentFile)) ?? throw new ErrorException(StatusCodes.Status404NotFound, "The specified document version was not found");

        var documentToUpdate = versionToUpdate.DocumentFile;

        //2. Editor must be the owner. 
        if (documentToUpdate.OwnerId != userId)
        {
            throw new ErrorException(StatusCodes.Status403Forbidden, "You do not have permission to edit this document");
        }

        //3. Status must be Draft or Rejected. 
        if (versionToUpdate.Status != StatusEnum.Draft && versionToUpdate.Status != StatusEnum.Rejected)
        {
            throw new ErrorException(StatusCodes.Status400BadRequest, $"Cannot edit a document with status '{versionToUpdate.Status}'");
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
                    $"This file already exists in the system as '{existingFile.DocumentFile.Title}' (Version: {existingFile.VersionName}, Status: {existingFile.Status}).");
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
        string tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));

        try
        {
            await using (var fs = new FileStream(tempFilePath, FileMode.Create)) { await file.CopyToAsync(fs); }

            // 1. Build the temporary Kernel Memory instance (same as before)
            var tempDocId = $"temp-analysis-{Guid.NewGuid()}";
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
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during single-prompt AI analysis for file: {FileName}", file.FileName);
        }
        finally
        {
            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
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
        // 1. Retrieve the document, ensuring its versions are included for status checking.
        var documentToDelete = await _unitOfWork.GetRepository<DocumentFile>()
            .SingleOrDefaultAsync(
                predicate: d => d.Id.ToString() == documentId,
                include: q => q.Include(d => d.DocumentVersions)
            ) ?? throw new ErrorException(StatusCodes.Status404NotFound, "Document not found.");

        // 2. Enforce Business Rules from SRS
        // BR-116: Check if the current user is the owner.
        if (documentToDelete.OwnerId != userId)
        {
            _logger.LogWarning("User attempted to delete a document they do not own owner");
            throw new ErrorException(StatusCodes.Status403Forbidden, "You do not have permission to delete this document.");
        }

        // A draft document should only have one version. We get that version to check its status.
        var versionToDelete = documentToDelete.DocumentVersions.FirstOrDefault();

        // BR-117: Check if the document's status is "Draft".
        if (versionToDelete == null || versionToDelete.Status != StatusEnum.Draft)
        {
            var currentStatus = versionToDelete?.Status.ToString() ?? "Unknown";
            _logger.LogWarning("Attempted to delete a document with status '{Status}', not 'Draft'.", currentStatus);
            throw new ErrorException(StatusCodes.Status400BadRequest, $"Only documents with a 'Draft' status can be deleted. Current status is '{currentStatus}'.");
        }

        // 3. Delete the physical file from Azure Storage.
        await _storageService.DeleteFileAsync(versionToDelete.FileName, StorageFolderConstant.Drafts);
        _logger.LogInformation("Deleted file from Azure Storage at path: {FilePath}", versionToDelete.FilePath);

        // 4. Delete the DocumentFile record from the database.
        // Due to cascade delete settings, this will also remove the associated DocumentVersion(s) and VersionTag(s).
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
            throw new ErrorException(StatusCodes.Status404NotFound, "Draft document not found.");
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
            throw new ErrorException(StatusCodes.Status404NotFound, "Rejected document not found.");
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
            throw new ErrorException(StatusCodes.Status404NotFound, "Official document not found for the given document file ID.");
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
}