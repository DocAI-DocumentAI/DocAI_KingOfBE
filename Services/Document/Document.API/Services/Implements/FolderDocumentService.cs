using Document.API.Payload.Request;
using Document.API.Payload.Response.Document;
using Document.API.Payload.Response.Folder;
using Document.Domain.Enums;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Models;
using Document.Infrastructure.Filter;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq.Expressions;
using Document.API.Constants;
using Shared.Exceptions;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service implementation for folder-based document operations
    /// </summary>
    public class FolderDocumentService : IFolderDocumentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFolderService _folderService;
        private readonly IFolderPermissionService _folderPermissionService;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<FolderDocumentService> _logger;

        public FolderDocumentService(
            IUnitOfWork unitOfWork,
            IFolderService folderService,
            IFolderPermissionService folderPermissionService,
            IGoogleDriveService googleDriveService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<FolderDocumentService> logger)
        {
            _unitOfWork = unitOfWork;
            _folderService = folderService;
            _folderPermissionService = folderPermissionService;
            _googleDriveService = googleDriveService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<FolderContentsResponse> BrowseFolderContentsAsync(FolderBrowseRequest request)
        {
            try
            {
                _logger.LogInformation("Browsing folder contents for folder {FolderId}", request.FolderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var response = new FolderContentsResponse();

                // Get current folder information
                if (!string.IsNullOrEmpty(request.FolderId))
                {
                    // ✅ SIMPLIFIED: Check folder access permission
                    var folderPermission = await _folderPermissionService.GetEffectivePermissionAsync(request.FolderId, userId, userDepartmentId ?? string.Empty);
                    if (folderPermission == null || !folderPermission.Value.Includes(PermissionType.View))
                    {
                        throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN,
                            FolderMessageConstant.Permissions.AccessDeniedToFolder);
                    }

                    var folderDetail = await _folderService.GetFolderByIdAsync(request.FolderId);
                    response.CurrentFolder = folderDetail;

                    // Get parent folder
                    if (!string.IsNullOrEmpty(folderDetail.ParentFolderId))
                    {
                        var parentFolder = await _folderService.GetFolderByIdAsync(folderDetail.ParentFolderId);
                        response.ParentFolder = MapToFolderSummary(parentFolder);
                    }

                    // Get breadcrumb
                    response.Breadcrumb = await _folderService.GetFolderBreadcrumbAsync(request.FolderId);

                    // Get user permissions
                    var userPermission = await _folderPermissionService.GetEffectivePermissionAsync(request.FolderId, userId, userDepartmentId);
                    response.UserPermissions = MapToActionPermissions(userPermission);
                }
                else
                {
                    // Root level browsing
                    if (request.IsPublic)
                    {
                        var publicTree = await _folderService.GetPublicFolderTreeAsync(true, 1);
                        response.CurrentFolder = new FolderDetailResponse
                        {
                            Id = "public-root",
                            Name = "Public Documents",
                            FullPath = "Public",
                            IsPublic = true,
                            CanCreateSubfolders = false,
                            CanUploadDocuments = false
                        };
                    }
                    else if (!string.IsNullOrEmpty(request.DepartmentId))
                    {
                        var deptTree = await _folderService.GetFolderTreeAsync(request.DepartmentId, true, 1);
                        response.CurrentFolder = new FolderDetailResponse
                        {
                            Id = $"dept-root-{request.DepartmentId}",
                            Name = $"Department Documents",
                            FullPath = "Department",
                            DepartmentId = request.DepartmentId,
                            IsPublic = false
                        };
                    }
                }

                // Get subfolders
                if (request.IncludeSubfolders)
                {
                    response.SubFolders = await GetAccessibleSubfoldersAsync(request.FolderId, userId, userDepartmentId, request);
                    response.TotalSubFolders = response.SubFolders.Count;
                }

                // Get documents
                if (request.IncludeDocuments)
                {
                    var documentsResult = await GetFolderDocumentsAsync(
                        request.FolderId ?? string.Empty,
                        request.DocumentPage,
                        request.DocumentPageSize,
                        request.DocumentStatus,
                        request.DocumentTypeId,
                        request.DocumentSortBy,
                        request.DocumentSortDirection);

                    response.Documents = documentsResult.Documents.Select(d => MapToDocumentSummary(d)).ToList();
                    response.TotalDocuments = documentsResult.TotalResults;
                    response.CurrentDocumentPage = documentsResult.CurrentPage;
                    response.DocumentPageSize = documentsResult.PageSize;
                    response.TotalDocumentPages = documentsResult.TotalPages;
                }

                // Set applied filters and sorting
                response.AppliedFilters = new FolderBrowseFilters
                {
                    DocumentStatus = request.DocumentStatus,
                    DocumentTypeId = request.DocumentTypeId,
                    IncludeSubfolders = request.IncludeSubfolders,
                    MaxDepth = request.MaxDepth
                };

                response.Sorting = new FolderBrowseSorting
                {
                    DocumentSortBy = request.DocumentSortBy,
                    DocumentSortDirection = request.DocumentSortDirection,
                    FolderSortBy = request.FolderSortBy,
                    FolderSortDirection = request.FolderSortDirection
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing folder contents for folder {FolderId}", request.FolderId);
                throw;
            }
        }

        public async Task<FolderDocumentSearchResponse> SearchDocumentsInFolderAsync(FolderDocumentSearchRequest request)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                _logger.LogInformation("Searching documents in folder {FolderId} with keyword '{Keyword}'", request.FolderId, request.Keyword);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // ✅ SIMPLIFIED: Check folder access permission
                var folderPermission = await _folderPermissionService.GetEffectivePermissionAsync(request.FolderId, userId, userDepartmentId ?? string.Empty);
                if (folderPermission == null || !folderPermission.Value.Includes(PermissionType.View))
                {
                    throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN,
                        FolderMessageConstant.Permissions.AccessDeniedToSearchInFolder);
                }

                // Get search folder information
                var searchFolder = await _folderService.GetFolderByIdAsync(request.FolderId);

                var response = new FolderDocumentSearchResponse
                {
                    SearchFolder = MapToFolderSummary(searchFolder),
                    SearchQuery = request.Keyword,
                    SearchType = request.SearchType,
                    IncludedSubfolders = request.IncludeSubfolders,
                    CurrentPage = request.Page,
                    PageSize = request.PageSize
                };

                // Build search filter
                var filter = new FullTextSearchFilter
                {
                    Keyword = request.Keyword,
                    FolderId = request.FolderId,
                    IncludeSubfolders = request.IncludeSubfolders,
                    Tags = request.Tags,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    SignedBy = request.SignedBy,
                    DocumentTypeId = request.DocumentTypeId,
                    DepartmentId = userDepartmentId // For access control
                };

                // Apply status filter
                var statusFilter = GetStatusFilter(request.Status);

                // Execute search using GetPagingListAsync instead of manual query building
                var documents = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
                    selector: dv => dv,
                    filter: filter,
                    predicate: statusFilter,
                    include: i => i.Include(dv => dv.DocumentFile)
                                  .Include(dv => dv.Folder)
                                  .Include(dv => dv.DocumentTags).ThenInclude(dt => dt.Tag),
                    orderBy: GetOrderByExpression(request.SortBy, request.SortDirection),
                    page: request.Page,
                    size: request.PageSize
                );

                // Map to response
                response.Documents = documents.Items.Select(dv => MapToDocumentSearchResult(dv, request.Keyword)).ToList();
                response.TotalResults = documents.Total;
                response.TotalPages = documents.TotalPages;

                // Get searched folders if including subfolders
                if (request.IncludeSubfolders)
                {
                    response.SearchedFolders = await GetSearchedFoldersAsync(request.FolderId, userId, userDepartmentId);
                }

                // Set applied filters
                response.AppliedFilters = new FolderSearchFilters
                {
                    Status = request.Status,
                    DocumentTypeId = request.DocumentTypeId,
                    Tags = request.Tags,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    SignedBy = request.SignedBy,
                    SortBy = request.SortBy,
                    SortDirection = request.SortDirection
                };

                stopwatch.Stop();
                response.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Search completed in {ElapsedMs}ms, found {ResultCount} documents",
                    stopwatch.ElapsedMilliseconds, response.TotalResults);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents in folder {FolderId}", request.FolderId);
                throw;
            }
        }

        public async Task<FolderDocumentSearchResponse> GetFolderDocumentsAsync(
            string folderId,
            int page = 1,
            int pageSize = 20,
            string? status = null,
            string? documentTypeId = null,
            string? sortBy = "LastUpdatedTime",
            string? sortDirection = "desc")
        {
            try
            {
                _logger.LogInformation("Getting ALL documents for folder {FolderId} (no pagination)", folderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var response = new FolderDocumentSearchResponse
                {
                    CurrentPage = 1,
                    PageSize = int.MaxValue, // ✅ FIXED: Return all documents
                    SearchType = "FolderBrowse"
                };

                // If no folder is specified, return empty result (root level has no documents)
                if (string.IsNullOrEmpty(folderId))
                {
                    _logger.LogInformation("No folder specified, returning empty document list for root level");
                    response.TotalResults = 0;
                    response.TotalPages = 0;
                    response.Documents = new List<DocumentSearchResultResponse>();
                    return response;
                }

                // Get folder information
                var folder = await _folderService.GetFolderByIdAsync(folderId);
                response.SearchFolder = MapToFolderSummary(folder);

                // Build predicate for filtering
                Expression<Func<DocumentVersion, bool>> predicate = dv => dv.FolderId == folderId;

                // Apply status filter
                if (!string.IsNullOrEmpty(status))
                {
                    var statusFilter = GetStatusFilter(status);
                    predicate = CombinePredicates(predicate, statusFilter);
                }

                // Apply document type filter
                if (!string.IsNullOrEmpty(documentTypeId))
                {
                    predicate = CombinePredicates(predicate, dv => dv.DocumentFile.DocumentTypeId == documentTypeId);
                }

                // Apply access control
                predicate = CombinePredicates(predicate, dv => dv.IsPublic || dv.DocumentFile.DepartmentId == userDepartmentId);

                // ✅ FIXED: Use GetListAsync to return ALL documents (no pagination)
                var documents = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
                    predicate: predicate,
                    include: i => i.Include(dv => dv.DocumentFile)
                                  .Include(dv => dv.Folder)
                                  .Include(dv => dv.DocumentTags).ThenInclude(dt => dt.Tag),
                    orderBy: GetOrderByExpression(sortBy, sortDirection)
                );

                // Map to response
                response.Documents = documents.Select(dv => MapToDocumentSearchResult(dv)).ToList();
                response.TotalResults = documents.Count;
                response.TotalPages = 1; // ✅ Always 1 page since we return all documents

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents for folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<List<DocumentSearchResultResponse>> GetRecentDocumentsAsync(
            string userId,
            string userDepartmentId,
            int limit = 10,
            string? departmentId = null)
        {
            try
            {
                _logger.LogInformation("Getting recent documents for user {UserId}", userId);

                // Build predicate for filtering
                Expression<Func<DocumentVersion, bool>> predicate = dv => dv.Status == StatusEnum.Approved;
                predicate = CombinePredicates(predicate, dv => dv.IsPublic || dv.DocumentFile.DepartmentId == userDepartmentId);

                // Apply department filter if specified
                if (!string.IsNullOrEmpty(departmentId))
                {
                    predicate = CombinePredicates(predicate, dv => dv.DocumentFile.DepartmentId == departmentId);
                }

                var recentDocuments = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
                    predicate: predicate,
                    include: i => i.Include(dv => dv.DocumentFile)
                                  .Include(dv => dv.Folder)
                                  .Include(dv => dv.DocumentTags).ThenInclude(dt => dt.Tag),
                    orderBy: q => q.OrderByDescending(dv => dv.LastUpdatedTime)
                );

                // Apply limit manually since GetListAsync doesn't support size parameter
                var limitedDocuments = recentDocuments.Take(limit).ToList();

                return limitedDocuments.Select(dv => MapToDocumentSearchResult(dv)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent documents for user {UserId}", userId);
                throw;
            }
        }

        public async Task<FolderDocumentStatistics> GetFolderDocumentStatisticsAsync(string folderId, bool includeSubfolders = false)
        {
            try
            {
                _logger.LogInformation("Getting document statistics for folder {FolderId}", folderId);

                var folder = await _folderService.GetFolderByIdAsync(folderId);

                var statistics = new FolderDocumentStatistics
                {
                    FolderId = folderId,
                    FolderName = folder.Name,
                    GeneratedAt = DateTime.UtcNow
                };

                Expression<Func<DocumentVersion, bool>> predicate;

                if (includeSubfolders)
                {
                    // Get all subfolders
                    var subfolders = await GetAllSubfoldersRecursiveAsync(folderId);
                    var folderIds = subfolders.Select(f => f.Id).Concat(new[] { folderId }).Where(id => !string.IsNullOrEmpty(id)).ToList();
                    predicate = dv => folderIds.Contains(dv.FolderId ?? string.Empty);
                    statistics.SubfoldersIncluded = subfolders.Count;
                }
                else
                {
                    predicate = dv => dv.FolderId == folderId;
                }

                var documents = await _unitOfWork.GetRepository<DocumentVersion>().GetListAsync(
                    predicate: predicate,
                    include: i => i.Include(dv => dv.DocumentFile)
                                  .Include(dv => dv.DocumentFile.DocumentType)
                );

                statistics.TotalDocuments = documents.Count;
                statistics.TotalFileSize = documents.Sum(dv => dv.FileSize);

                if (documents.Any())
                {
                    statistics.MostRecentDocument = documents.Max(dv => dv.LastUpdatedTime);
                    statistics.OldestDocument = documents.Min(dv => dv.CreatedTime);
                }

                // Group by status
                statistics.DocumentsByStatus = documents
                    .GroupBy(dv => dv.Status.ToString())
                    .ToDictionary(g => g.Key, g => g.Count());

                // Group by type
                statistics.DocumentsByType = documents
                    .GroupBy(dv => dv.DocumentFile.DocumentType?.Name ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count());

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document statistics for folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<bool> MoveDocumentToFolderAsync(string documentVersionId, string targetFolderId)
        {
            try
            {
                _logger.LogInformation("Moving document {DocumentVersionId} to folder {FolderId}", documentVersionId, targetFolderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // ✅ DEPARTMENT BOUNDARY VALIDATION: Prevent moving documents outside department
                await ValidateDepartmentBoundaryForDocumentMoveAsync(targetFolderId, userId, userDepartmentId);

                var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.Id == documentVersionId,
                        include: i => i.Include(dv => dv.Folder)
                    );

                if (documentVersion == null)
                {
                    throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND,
                        string.Format(FolderMessageConstant.System.DocumentVersionNotFound, documentVersionId));
                }

                // ✅ Check SOURCE folder permission (if document is in a folder)
                if (!string.IsNullOrEmpty(documentVersion.FolderId))
                {
                    var sourcePermission = await _folderPermissionService.GetEffectivePermissionAsync(
                        documentVersion.FolderId, userId, userDepartmentId ?? string.Empty);

                    if (sourcePermission == null || !sourcePermission.Value.Includes(PermissionType.Edit))
                    {
                        throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN,
                            FolderMessageConstant.Permissions.AccessDeniedToMoveFromSourceFolder);
                    }
                }

                // ✅ Check TARGET folder permission
                var targetPermission = await _folderPermissionService.GetEffectivePermissionAsync(
                    targetFolderId, userId, userDepartmentId ?? string.Empty);

                if (targetPermission == null || !targetPermission.Value.Includes(PermissionType.Edit))
                {
                    throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN,
                        FolderMessageConstant.Permissions.AccessDeniedToMoveToTargetFolder);
                }

                // ✅ Move file in Google Drive if it exists
                if (!string.IsNullOrEmpty(documentVersion.GoogleDriveFileId))
                {
                    var targetFolder = await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(predicate: f => f.Id == targetFolderId);

                    if (targetFolder != null)
                    {
                        // Move file to target folder in Google Drive
                        await _googleDriveService.MoveFileToFolderAsync(
                            documentVersion.GoogleDriveFileId,
                            targetFolder.GoogleDriveFolderId);

                        _logger.LogDebug("Moved file {FileId} to Google Drive folder {FolderId}",
                            documentVersion.GoogleDriveFileId, targetFolder.GoogleDriveFolderId);
                    }
                }

                // ✅ Update database
                documentVersion.FolderId = targetFolderId;
                documentVersion.LastUpdatedBy = userId;
                documentVersion.LastUpdatedTime = DateTime.UtcNow;

                await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(documentVersion);
                await _unitOfWork.CommitAsync();

                // ✅ Update RAG index if approved document
                if (documentVersion.Status == StatusEnum.Approved)
                {
                    try
                    {
                        // TODO: Add RAG service integration when available
                        _logger.LogDebug("Document {DocumentId} moved - RAG index update needed", documentVersionId);
                        // await _documentRAGService.RemoveDocumentFromIndexAsync(documentVersionId);
                        // await _documentRAGService.IndexDocumentWithFolderContextAsync(documentVersion);
                    }
                    catch (Exception ragEx)
                    {
                        _logger.LogWarning(ragEx, "Failed to update RAG index for moved document {DocumentId}", documentVersionId);
                        // Don't fail the operation for RAG issues
                    }
                }

                _logger.LogInformation("Successfully moved document {DocumentVersionId} to folder {FolderId}", documentVersionId, targetFolderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving document {DocumentVersionId} to folder {FolderId}", documentVersionId, targetFolderId);
                throw;
            }
        }

        public async Task<int> BulkMoveDocumentsToFolderAsync(List<string> documentVersionIds, string targetFolderId)
        {
            try
            {
                _logger.LogInformation("Bulk moving {Count} documents to folder {FolderId}", documentVersionIds.Count, targetFolderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // Check target folder permissions
                if (!await _folderPermissionService.GetEffectivePermissionAsync(targetFolderId, userId, userDepartmentId ?? string.Empty).ContinueWith(t => t.Result?.Includes(PermissionType.Edit) == true))
                {
                    throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN,
                        FolderMessageConstant.Permissions.AccessDeniedToUploadToTargetFolder);
                }

                var documentVersions = await _unitOfWork.GetRepository<DocumentVersion>()
                    .GetListAsync(predicate: dv => documentVersionIds.Contains(dv.Id));

                int movedCount = 0;
                foreach (var documentVersion in documentVersions)
                {
                    documentVersion.FolderId = targetFolderId;
                    documentVersion.LastUpdatedBy = userId;
                    documentVersion.LastUpdatedTime = DateTime.UtcNow;

                    await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(documentVersion);
                    movedCount++;
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully moved {MovedCount} documents to folder {FolderId}", movedCount, targetFolderId);
                return movedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk moving documents to folder {FolderId}", targetFolderId);
                throw;
            }
        }

        public async Task<List<FolderBreadcrumbResponse>> GetDocumentFolderPathAsync(string documentVersionId)
        {
            try
            {
                var documentVersion = await _unitOfWork.GetRepository<DocumentVersion>()
                    .SingleOrDefaultAsync(
                        predicate: dv => dv.Id == documentVersionId,
                        include: i => i.Include(dv => dv.Folder)
                    );

                if (documentVersion?.Folder == null)
                {
                    return new List<FolderBreadcrumbResponse>();
                }

                return await _folderService.GetFolderBreadcrumbAsync(documentVersion.Folder.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder path for document {DocumentVersionId}", documentVersionId);
                throw;
            }
        }

        public async Task<FolderDocumentSearchResponse> SearchAcrossFoldersAsync(
            List<string> folderIds,
            string? keyword,
            bool includeSubfolders = false,
            FolderSearchFilters? filters = null,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                _logger.LogInformation("Searching across {FolderCount} folders with keyword '{Keyword}'", folderIds.Count, keyword);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var response = new FolderDocumentSearchResponse
                {
                    SearchQuery = keyword,
                    SearchType = "CrossFolder",
                    IncludedSubfolders = includeSubfolders,
                    CurrentPage = page,
                    PageSize = pageSize
                };

                // Build predicate for folder filtering
                Expression<Func<DocumentVersion, bool>> predicate;
                if (includeSubfolders)
                {
                    var allFolderIds = new List<string>(folderIds);
                    foreach (var folderId in folderIds)
                    {
                        var subfolders = await GetAllSubfoldersRecursiveAsync(folderId);
                        allFolderIds.AddRange(subfolders.Select(f => f.Id).Where(id => !string.IsNullOrEmpty(id)));
                    }
                    var nonNullFolderIds = allFolderIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
                    predicate = dv => nonNullFolderIds.Contains(dv.FolderId ?? string.Empty);
                }
                else
                {
                    var nonNullFolderIds = folderIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
                    predicate = dv => nonNullFolderIds.Contains(dv.FolderId ?? string.Empty);
                }

                // Apply keyword filter
                if (!string.IsNullOrEmpty(keyword))
                {
                    predicate = CombinePredicates(predicate, dv =>
                        dv.Title.Contains(keyword) ||
                        dv.Summary.Contains(keyword) ||
                        dv.VersionName.Contains(keyword));
                }

                // Apply additional filters
                if (filters != null)
                {
                    predicate = ApplySearchFiltersToPredicate(predicate, filters);
                }

                // Apply access control
                predicate = CombinePredicates(predicate, dv => dv.IsPublic || dv.DocumentFile.DepartmentId == userDepartmentId);

                // Use GetPagingListAsync with proper parameters
                var documents = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
                    selector: dv => dv,
                    filter: null!,
                    predicate: predicate,
                    include: i => i.Include(dv => dv.DocumentFile)
                                  .Include(dv => dv.Folder)
                                  .Include(dv => dv.DocumentTags).ThenInclude(dt => dt.Tag),
                    orderBy: GetOrderByExpression(filters?.SortBy, filters?.SortDirection),
                    page: page,
                    size: pageSize
                );

                // Map to response
                response.Documents = documents.Items.Select(dv => MapToDocumentSearchResult(dv, keyword)).ToList();
                response.TotalResults = documents.Total;
                response.TotalPages = documents.TotalPages;

                // Get searched folders
                response.SearchedFolders = await GetFolderSummariesAsync(folderIds);

                stopwatch.Stop();
                response.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching across folders");
                throw;
            }
        }

        public async Task<FolderDocumentSearchResponse> GetDocumentsByPathPatternAsync(
            string pathPattern,
            string? departmentId = null,
            bool? isPublic = null,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                _logger.LogInformation("Getting documents by path pattern '{PathPattern}'", pathPattern);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var response = new FolderDocumentSearchResponse
                {
                    SearchQuery = pathPattern,
                    SearchType = "PathPattern",
                    CurrentPage = page,
                    PageSize = pageSize
                };

                // Build predicate
                Expression<Func<DocumentVersion, bool>> predicate = dv => dv.Folder != null && dv.Folder.FullPath.Contains(pathPattern);

                // Apply department filter
                if (!string.IsNullOrEmpty(departmentId))
                {
                    predicate = CombinePredicates(predicate, dv => dv.DocumentFile.DepartmentId == departmentId);
                }

                // Apply public filter
                if (isPublic.HasValue)
                {
                    predicate = CombinePredicates(predicate, dv => dv.IsPublic == isPublic.Value);
                }

                // Apply access control
                predicate = CombinePredicates(predicate, dv => dv.IsPublic || dv.DocumentFile.DepartmentId == userDepartmentId);

                // Use GetPagingListAsync with proper parameters
                var documents = await _unitOfWork.GetRepository<DocumentVersion>().GetPagingListAsync(
                    selector: dv => dv,
                    filter: null!,
                    predicate: predicate,
                    include: i => i.Include(dv => dv.DocumentFile)
                                  .Include(dv => dv.Folder)
                                  .Include(dv => dv.DocumentTags).ThenInclude(dt => dt.Tag),
                    orderBy: q => q.OrderByDescending(dv => dv.LastUpdatedTime),
                    page: page,
                    size: pageSize
                );

                // Map to response
                response.Documents = documents.Items.Select(dv => MapToDocumentSearchResult(dv)).ToList();
                response.TotalResults = documents.Total;
                response.TotalPages = documents.TotalPages;

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents by path pattern '{PathPattern}'", pathPattern);
                throw;
            }
        }

        #region Helper Methods

        private async Task<List<FolderSummaryResponse>> GetAccessibleSubfoldersAsync(string? parentFolderId, string userId, string? userDepartmentId, FolderBrowseRequest request)
        {
            var subfolders = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(
                    predicate: f => f.ParentFolderId == parentFolderId && !f.IsDeleted,
                    include: i => i.Include(f => f.FolderPermissions.Where(fp => fp.IsActive))
                );

            var accessibleFolders = new List<FolderSummaryResponse>();

            foreach (var folder in subfolders)
            {
                var hasAccess = await _folderPermissionService.GetEffectivePermissionAsync(folder.Id, userId, userDepartmentId ?? string.Empty);
                if (hasAccess?.Includes(PermissionType.View) == true)
                {
                    accessibleFolders.Add(MapToFolderSummary(folder));
                }
            }

            // Apply sorting
            return ApplyFolderSorting(accessibleFolders, request.FolderSortBy, request.FolderSortDirection);
        }

        private async Task<List<FolderSummaryResponse>> GetSearchedFoldersAsync(string folderId, string userId, string? userDepartmentId)
        {
            var subfolders = await GetAllSubfoldersRecursiveAsync(folderId);
            var searchedFolders = new List<FolderSummaryResponse>();

            foreach (var folder in subfolders)
            {
                var hasAccess = await _folderPermissionService.GetEffectivePermissionAsync(folder.Id, userId, userDepartmentId ?? string.Empty);
                if (hasAccess?.Includes(PermissionType.View) == true)
                {
                    searchedFolders.Add(MapToFolderSummary(folder));
                }
            }

            return searchedFolders;
        }

        private async Task<List<Folder>> GetAllSubfoldersRecursiveAsync(string folderId)
        {
            var allSubfolders = new List<Folder>();
            var directSubfolders = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(predicate: f => f.ParentFolderId == folderId && !f.IsDeleted);

            allSubfolders.AddRange(directSubfolders);

            foreach (var subfolder in directSubfolders)
            {
                var nestedSubfolders = await GetAllSubfoldersRecursiveAsync(subfolder.Id);
                allSubfolders.AddRange(nestedSubfolders);
            }

            return allSubfolders;
        }

        private async Task<List<FolderSummaryResponse>> GetFolderSummariesAsync(List<string> folderIds)
        {
            var folders = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(predicate: f => folderIds.Contains(f.Id) && !f.IsDeleted);

            return folders.Select(f => MapToFolderSummary(f)).ToList();
        }

        private static System.Linq.Expressions.Expression<Func<DocumentVersion, bool>> GetStatusFilter(string? status)
        {
            if (string.IsNullOrEmpty(status))
            {
                return dv => dv.Status == StatusEnum.Approved; // Default to approved documents
            }

            return status.ToLower() switch
            {
                "draft" => dv => dv.Status == StatusEnum.Draft,
                "pending" => dv => dv.Status == StatusEnum.Pending,
                "approved" => dv => dv.Status == StatusEnum.Approved,
                "rejected" => dv => dv.Status == StatusEnum.Rejected,
                "all" => dv => true,
                _ => dv => dv.Status == StatusEnum.Approved
            };
        }

        private static IQueryable<DocumentVersion> ApplySorting(IQueryable<DocumentVersion> query, string? sortBy, string? sortDirection)
        {
            var isDescending = sortDirection?.ToLower() == "desc";

            return sortBy?.ToLower() switch
            {
                "title" => isDescending ? query.OrderByDescending(dv => dv.Title) : query.OrderBy(dv => dv.Title),
                "createdtime" => isDescending ? query.OrderByDescending(dv => dv.CreatedTime) : query.OrderBy(dv => dv.CreatedTime),
                "lastupdatedtime" => isDescending ? query.OrderByDescending(dv => dv.LastUpdatedTime) : query.OrderBy(dv => dv.LastUpdatedTime),
                "filesize" => isDescending ? query.OrderByDescending(dv => dv.FileSize) : query.OrderBy(dv => dv.FileSize),
                "status" => isDescending ? query.OrderByDescending(dv => dv.Status) : query.OrderBy(dv => dv.Status),
                _ => query.OrderByDescending(dv => dv.LastUpdatedTime) // Default sorting
            };
        }

        private static List<FolderSummaryResponse> ApplyFolderSorting(List<FolderSummaryResponse> folders, string? sortBy, string? sortDirection)
        {
            var isDescending = sortDirection?.ToLower() == "desc";

            return sortBy?.ToLower() switch
            {
                "name" => isDescending ? folders.OrderByDescending(f => f.Name).ToList() : folders.OrderBy(f => f.Name).ToList(),
                "createdtime" => isDescending ? folders.OrderByDescending(f => f.CreatedTime).ToList() : folders.OrderBy(f => f.CreatedTime).ToList(),
                "documentcount" => isDescending ? folders.OrderByDescending(f => f.DocumentCount).ToList() : folders.OrderBy(f => f.DocumentCount).ToList(),
                _ => folders.OrderBy(f => f.Name).ToList() // Default sorting
            };
        }

        private static IQueryable<DocumentVersion> ApplySearchFilters(IQueryable<DocumentVersion> query, FolderSearchFilters filters)
        {
            if (!string.IsNullOrEmpty(filters.Status))
            {
                query = query.Where(GetStatusFilter(filters.Status));
            }

            if (!string.IsNullOrEmpty(filters.DocumentTypeId))
            {
                query = query.Where(dv => dv.DocumentFile.DocumentTypeId == filters.DocumentTypeId);
            }

            if (filters.Tags?.Any() == true)
            {
                query = query.Where(dv => dv.DocumentTags.Any(dt => filters.Tags.Contains(dt.Tag.Name)));
            }

            if (filters.FromDate.HasValue)
            {
                query = query.Where(dv => dv.CreatedTime >= filters.FromDate);
            }

            if (filters.ToDate.HasValue)
            {
                query = query.Where(dv => dv.CreatedTime <= filters.ToDate);
            }

            if (!string.IsNullOrEmpty(filters.SignedBy))
            {
                query = query.Where(dv => dv.SignedBy != null && dv.SignedBy.Contains(filters.SignedBy));
            }

            return query;
        }

        private static FolderSummaryResponse MapToFolderSummary(FolderDetailResponse folder)
        {
            return new FolderSummaryResponse
            {
                Id = folder.Id,
                Name = folder.Name,
                FullPath = folder.FullPath,
                DepartmentId = folder.DepartmentId,
                IsPublic = folder.IsPublic,
                IsSystemFolder = folder.IsSystemFolder,
                FolderType = folder.FolderType,
                DocumentCount = folder.DocumentCount,
                SubFolderCount = folder.SubFolderCount,
                CreatedTime = folder.CreatedTime,
                CreatedBy = folder.CreatedBy
            };
        }

        private static FolderSummaryResponse MapToFolderSummary(Folder folder)
        {
            return new FolderSummaryResponse
            {
                Id = folder.Id,
                Name = folder.Name,
                FullPath = folder.FullPath,
                DepartmentId = folder.DepartmentId,
                IsPublic = folder.IsPublic,
                IsSystemFolder = folder.IsSystemFolder,
                FolderType = folder.FolderType,
                DocumentCount = folder.DocumentCount,
                SubFolderCount = folder.SubFolderCount,
                CreatedTime = folder.CreatedTime,
                CreatedBy = folder.CreatedBy
            };
        }

        private static DocumentSummaryResponse MapToDocumentSummary(DocumentSearchResultResponse searchResult)
        {
            return new DocumentSummaryResponse
            {
                Id = searchResult.Id,
                DocumentFileId = searchResult.DocumentFileId, // ✅ ADDED: Document File ID
                VersionId = searchResult.VersionId,           // ✅ ADDED: Version ID
                Title = searchResult.Title,
                VersionName = searchResult.VersionName,
                Summary = searchResult.Summary,
                Status = searchResult.Status,
                DocumentType = searchResult.DocumentType,
                FileSize = searchResult.FileSize,
                CreatedTime = searchResult.CreatedTime,
                LastUpdatedTime = searchResult.LastUpdatedTime,
                CreatedBy = searchResult.CreatedBy,
                Tags = searchResult.Tags,

                // ✅ FIXED: Include missing fields from search result
                IsPublic = searchResult.IsPublic,
                DepartmentId = searchResult.DepartmentId,
                FileType = searchResult.FileType, // ✅ Now includes file type
                SignedBy = searchResult.SignedBy,
                EffectiveFrom = searchResult.EffectiveFrom,
                EffectiveUntil = searchResult.EffectiveUntil
            };
        }

        private static DocumentSearchResultResponse MapToDocumentSearchResult(DocumentVersion documentVersion, string? searchKeyword = null)
        {
            var result = new DocumentSearchResultResponse
            {
                Id = documentVersion.Id,
                Title = documentVersion.Title,
                VersionName = documentVersion.VersionName,
                Summary = documentVersion.Summary,
                Status = documentVersion.Status.ToString(),
                FileSize = documentVersion.FileSize,
                CreatedTime = documentVersion.CreatedTime,
                LastUpdatedTime = documentVersion.LastUpdatedTime ?? DateTime.UtcNow,
                CreatedBy = documentVersion.CreatedBy,
                Tags = documentVersion.DocumentTags?.Select(dt => dt.Tag.Name).ToList() ?? new List<string>(),

                // ✅ FIXED: Add missing fields
                IsPublic = documentVersion.IsPublic,
                DepartmentId = documentVersion.DocumentFile?.DepartmentId,
                FileType = GetFileTypeFromFileName(documentVersion.FileName), // ✅ Extract file type from filename
                SignedBy = documentVersion.SignedBy,
                EffectiveFrom = documentVersion.EffectiveFrom,
                EffectiveUntil = documentVersion.EffectiveUntil,

                // ✅ ADDED: Document File ID and Version ID
                DocumentFileId = documentVersion.DocumentFileId,
                VersionId = documentVersion.Id // Version ID is the same as document version ID
            };

            if (documentVersion.DocumentFile?.DocumentType != null)
            {
                result.DocumentType = documentVersion.DocumentFile.DocumentType.Name;
            }

            if (documentVersion.Folder != null)
            {
                result.ContainingFolder = MapToFolderSummary(documentVersion.Folder);
            }

            // Add highlighted snippets if search keyword is provided
            if (!string.IsNullOrEmpty(searchKeyword))
            {
                result.HighlightedSnippets = GenerateHighlightedSnippets(documentVersion, searchKeyword);
                result.MatchingFields = GetMatchingFields(documentVersion, searchKeyword);
            }

            return result;
        }

        private static List<string> GenerateHighlightedSnippets(DocumentVersion documentVersion, string keyword)
        {
            var snippets = new List<string>();
            var keywordLower = keyword.ToLower();

            // Check title
            if (documentVersion.Title.ToLower().Contains(keywordLower))
            {
                snippets.Add($"Title: {HighlightKeyword(documentVersion.Title, keyword)}");
            }

            // Check summary
            if (!string.IsNullOrEmpty(documentVersion.Summary) && documentVersion.Summary.ToLower().Contains(keywordLower))
            {
                var snippet = GetTextSnippet(documentVersion.Summary, keyword, 100);
                snippets.Add($"Summary: {HighlightKeyword(snippet, keyword)}");
            }

            // Check version name
            if (!string.IsNullOrEmpty(documentVersion.VersionName) && documentVersion.VersionName.ToLower().Contains(keywordLower))
            {
                snippets.Add($"Version: {HighlightKeyword(documentVersion.VersionName, keyword)}");
            }

            return snippets;
        }

        private static List<string> GetMatchingFields(DocumentVersion documentVersion, string keyword)
        {
            var matchingFields = new List<string>();
            var keywordLower = keyword.ToLower();

            if (documentVersion.Title.ToLower().Contains(keywordLower))
                matchingFields.Add("Title");

            if (!string.IsNullOrEmpty(documentVersion.Summary) && documentVersion.Summary.ToLower().Contains(keywordLower))
                matchingFields.Add("Summary");

            if (!string.IsNullOrEmpty(documentVersion.VersionName) && documentVersion.VersionName.ToLower().Contains(keywordLower))
                matchingFields.Add("Version");

            if (documentVersion.DocumentTags?.Any(dt => dt.Tag.Name.ToLower().Contains(keywordLower)) == true)
                matchingFields.Add("Tags");

            return matchingFields;
        }

        private static string HighlightKeyword(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return text;

            // Simple highlighting - in a real implementation, you might use HTML tags
            return text.Replace(keyword, $"**{keyword}**", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetTextSnippet(string text, string keyword, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return text;

            var keywordIndex = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (keywordIndex == -1)
                return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;

            var startIndex = Math.Max(0, keywordIndex - maxLength / 2);
            var length = Math.Min(maxLength, text.Length - startIndex);

            var snippet = text.Substring(startIndex, length);

            if (startIndex > 0)
                snippet = "..." + snippet;

            if (startIndex + length < text.Length)
                snippet = snippet + "...";

            return snippet;
        }

        private static FolderActionPermissions? MapToActionPermissions(PermissionType? permission)
        {
            if (permission == null)
                return null;

            return new FolderActionPermissions
            {
                CanView = permission.Value.Includes(PermissionType.View),
                CanCreateSubfolder = permission.Value.Includes(PermissionType.Edit),
                CanUploadDocument = permission.Value.Includes(PermissionType.Edit),
                CanEditFolder = permission.Value.Includes(PermissionType.Edit),
                CanDeleteFolder = permission.Value.Includes(PermissionType.Delete),
                CanManagePermissions = permission.Value.Includes(PermissionType.Manage),
                CanMoveFolder = permission.Value.Includes(PermissionType.Manage)
            };
        }

        private static Expression<Func<DocumentVersion, bool>> CombinePredicates(
            Expression<Func<DocumentVersion, bool>> first,
            Expression<Func<DocumentVersion, bool>> second)
        {
            var parameter = Expression.Parameter(typeof(DocumentVersion), "dv");
            var firstBody = ReplaceParameter(first.Body, first.Parameters[0], parameter);
            var secondBody = ReplaceParameter(second.Body, second.Parameters[0], parameter);
            var combined = Expression.AndAlso(firstBody, secondBody);
            return Expression.Lambda<Func<DocumentVersion, bool>>(combined, parameter);
        }

        private static Expression<Func<DocumentVersion, bool>> ApplySearchFiltersToPredicate(
            Expression<Func<DocumentVersion, bool>> predicate,
            FolderSearchFilters filters)
        {
            if (!string.IsNullOrEmpty(filters.Status))
            {
                var statusFilter = GetStatusFilter(filters.Status);
                predicate = CombinePredicates(predicate, statusFilter);
            }

            if (!string.IsNullOrEmpty(filters.DocumentTypeId))
            {
                predicate = CombinePredicates(predicate, dv => dv.DocumentFile.DocumentTypeId == filters.DocumentTypeId);
            }

            if (filters.Tags?.Any() == true)
            {
                predicate = CombinePredicates(predicate, dv => dv.DocumentTags.Any(dt => filters.Tags.Contains(dt.Tag.Name)));
            }

            if (filters.FromDate.HasValue)
            {
                predicate = CombinePredicates(predicate, dv => dv.CreatedTime >= filters.FromDate);
            }

            if (filters.ToDate.HasValue)
            {
                predicate = CombinePredicates(predicate, dv => dv.CreatedTime <= filters.ToDate);
            }

            if (!string.IsNullOrEmpty(filters.SignedBy))
            {
                predicate = CombinePredicates(predicate, dv => dv.SignedBy != null && dv.SignedBy.Contains(filters.SignedBy));
            }

            return predicate;
        }

        private static Expression ReplaceParameter(Expression expression, ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
        }

        private static Func<IQueryable<DocumentVersion>, IOrderedQueryable<DocumentVersion>> GetOrderByExpression(string? sortBy, string? sortDirection)
        {
            var isDescending = sortDirection?.ToLower() == "desc";

            return sortBy?.ToLower() switch
            {
                "title" => isDescending ? q => q.OrderByDescending(dv => dv.Title) : q => q.OrderBy(dv => dv.Title),
                "createdtime" => isDescending ? q => q.OrderByDescending(dv => dv.CreatedTime) : q => q.OrderBy(dv => dv.CreatedTime),
                "lastupdatedtime" => isDescending ? q => q.OrderByDescending(dv => dv.LastUpdatedTime) : q => q.OrderBy(dv => dv.LastUpdatedTime),
                "filesize" => isDescending ? q => q.OrderByDescending(dv => dv.FileSize) : q => q.OrderBy(dv => dv.FileSize),
                "status" => isDescending ? q => q.OrderByDescending(dv => dv.Status) : q => q.OrderBy(dv => dv.Status),
                _ => q => q.OrderByDescending(dv => dv.LastUpdatedTime) // Default sorting
            };
        }

        /// <summary>
        /// ✅ DEPARTMENT BOUNDARY VALIDATION: Prevent moving documents outside department
        /// </summary>
        private async Task ValidateDepartmentBoundaryForDocumentMoveAsync(string targetFolderId, string userId, string? userDepartmentId)
        {
            try
            {
                // Admins can move documents anywhere
                var userRole = JwtTokenHelper.GetUserRole(_httpContextAccessor);
                if (userRole == "Admin")
                {
                    return; // Admins have no restrictions
                }

                // Get target folder and validate department boundary
                var targetFolder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(predicate: f => f.Id == targetFolderId && !f.IsDeleted);

                if (targetFolder == null)
                {
                    throw new ErrorException(StatusCodes.Status404NotFound, ErrorCode.NOT_FOUND,
                        FolderMessageConstant.System.TargetFolderNotFound);
                }

                // Check if target folder is within user's department or public
                if (!targetFolder.IsPublic && targetFolder.DepartmentId != userDepartmentId)
                {
                    // Get department names for better error message
                    var targetDepartmentName = await GetDepartmentNameAsync(targetFolder.DepartmentId);
                    var userDepartmentName = await GetDepartmentNameAsync(userDepartmentId);

                    throw new ErrorException(StatusCodes.Status403Forbidden, ErrorCode.FORBIDDEN,
                        string.Format(FolderMessageConstant.System.AccessDeniedCannotMoveDocumentsOutsideDepartment,
                            targetDepartmentName, userDepartmentName));
                }

                _logger.LogInformation("Department boundary validation passed for user {UserId} to move document to folder {FolderId}",
                    userId, targetFolderId);
            }
            catch (Exception ex) when (!(ex is UnauthorizedAccessException || ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error validating department boundary for document move by user {UserId}", userId);
                throw new ErrorException(StatusCodes.Status500InternalServerError, ErrorCode.INTERNAL_SERVER_ERROR,
                    FolderMessageConstant.System.ErrorValidatingDepartmentAccessForDocumentMove);
            }
        }

        /// <summary>
        /// Helper method to get department name for error messages
        /// </summary>
        private async Task<string> GetDepartmentNameAsync(string? departmentId)
        {
            if (string.IsNullOrEmpty(departmentId))
            {
                return "Public";
            }

            // Try to get department name from folder structure
            var departmentFolder = await _unitOfWork.GetRepository<Folder>()
                .SingleOrDefaultAsync(predicate: f => f.DepartmentId == departmentId && f.ParentFolderId == null && !f.IsDeleted);

            return departmentFolder?.Name ?? $"Department-{departmentId}";
        }

        /// <summary>
        /// ✅ FIXED: Extract file type from filename
        /// </summary>
        private static string? GetFileTypeFromFileName(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            var extension = Path.GetExtension(fileName);
            return string.IsNullOrEmpty(extension) ? null : extension.TrimStart('.').ToUpperInvariant();
        }

        #endregion
    }

    internal class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }
    }
}
