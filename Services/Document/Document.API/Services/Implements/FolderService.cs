using Document.API.Constants;
using Document.API.Models;
using Document.API.Payload.Request.Folder;
using Document.API.Payload.Response.Folder;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Models;
using Document.Domain.Enums;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Diagnostics;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service implementation for folder management operations
    /// </summary>
    public class FolderService : IFolderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<FolderService> _logger;
        private readonly IFolderPermissionEnrichmentService _folderPermissionEnrichmentService;

        public FolderService(
            IUnitOfWork unitOfWork,
            IGoogleDriveService googleDriveService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<FolderService> logger,
            IFolderPermissionEnrichmentService folderPermissionEnrichmentService)
        {
            _unitOfWork = unitOfWork;
            _googleDriveService = googleDriveService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _folderPermissionEnrichmentService = folderPermissionEnrichmentService;
        }

        public async Task<FolderTreeResponse> GetFolderTreeAsync(string departmentId, bool includeSystemFolders = true, int? maxDepth = null)
        {
            try
            {
                _logger.LogInformation("Getting folder tree for department {DepartmentId}", departmentId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // Check if user has access to this department
                if (departmentId != userDepartmentId && !await IsUserAdminOrManagerAsync(userId))
                {
                    throw new UnauthorizedAccessException("Access denied to department folders");
                }

                // Get root folders for the department
                var rootFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(
                        predicate: f => f.DepartmentId == departmentId && f.ParentFolderId == null && !f.IsDeleted,
                        include: i => i.Include(f => f.SubFolders.Where(sf => !sf.IsDeleted))
                                      .Include(f => f.Documents.Where(d => !string.IsNullOrEmpty(d.FolderId)))
                    );

                if (!rootFolders.Any())
                {
                    // Initialize department folders if they don't exist
                    await InitializeDepartmentFoldersAsync(departmentId, "Department");
                    rootFolders = await _unitOfWork.GetRepository<Folder>()
                        .GetListAsync(
                            predicate: f => f.DepartmentId == departmentId && f.ParentFolderId == null && !f.IsDeleted,
                            include: i => i.Include(f => f.SubFolders.Where(sf => !sf.IsDeleted))
                                          .Include(f => f.Documents.Where(d => !string.IsNullOrEmpty(d.FolderId)))
                        );
                }

                var rootFolder = rootFolders.FirstOrDefault();
                if (rootFolder == null)
                {
                    throw new KeyNotFoundException($"No folders found for department {departmentId}");
                }

                var folderNode = await BuildFolderNodeAsync(rootFolder, userId, userDepartmentId, includeSystemFolders, maxDepth, 0);

                return new FolderTreeResponse
                {
                    RootFolder = folderNode,
                    TotalFolders = await CountFoldersInTreeAsync(rootFolder.Id, includeSystemFolders),
                    MaxDepth = await GetMaxDepthAsync(rootFolder.Id),
                    IncludesSystemFolders = includeSystemFolders,
                    DepartmentId = departmentId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder tree for department {DepartmentId}", departmentId);
                throw;
            }
        }

        public async Task<FolderTreeResponse> GetPublicFolderTreeAsync(bool includeSystemFolders = true, int? maxDepth = null)
        {
            try
            {
                _logger.LogInformation("Getting public folder tree");

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // Get public root folders
                var rootFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(
                        predicate: f => f.IsPublic && f.ParentFolderId == null && !f.IsDeleted,
                        include: i => i.Include(f => f.SubFolders.Where(sf => !sf.IsDeleted))
                                      .Include(f => f.Documents.Where(d => !string.IsNullOrEmpty(d.FolderId)))
                    );

                if (!rootFolders.Any())
                {
                    // Initialize public folders if they don't exist
                    await InitializePublicFoldersAsync();
                    rootFolders = await _unitOfWork.GetRepository<Folder>()
                        .GetListAsync(
                            predicate: f => f.IsPublic && f.ParentFolderId == null && !f.IsDeleted,
                            include: i => i.Include(f => f.SubFolders.Where(sf => !sf.IsDeleted))
                                          .Include(f => f.Documents.Where(d => !string.IsNullOrEmpty(d.FolderId)))
                        );
                }

                var rootFolder = rootFolders.FirstOrDefault();
                if (rootFolder == null)
                {
                    throw new KeyNotFoundException("No public folders found");
                }

                var folderNode = await BuildFolderNodeAsync(rootFolder, userId, userDepartmentId, includeSystemFolders, maxDepth, 0);

                return new FolderTreeResponse
                {
                    RootFolder = folderNode,
                    TotalFolders = await CountFoldersInTreeAsync(rootFolder.Id, includeSystemFolders),
                    MaxDepth = await GetMaxDepthAsync(rootFolder.Id),
                    IncludesSystemFolders = includeSystemFolders,
                    DepartmentId = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting public folder tree");
                throw;
            }
        }

        public async Task<FolderDetailResponse> GetFolderByIdAsync(string folderId)
        {
            try
            {
                _logger.LogInformation("Getting folder details for {FolderId}", folderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(
                        predicate: f => f.Id == folderId && !f.IsDeleted,
                        include: i => i.Include(f => f.ParentFolder)
                                      .Include(f => f.SubFolders.Where(sf => !sf.IsDeleted))
                                      .Include(f => f.Documents.Where(d => !string.IsNullOrEmpty(d.FolderId)))
                                      .Include(f => f.FolderPermissions.Where(fp => fp.IsActive))
                    );

                if (folder == null)
                {
                    throw new KeyNotFoundException($"Folder with ID {folderId} not found");
                }

                // Check access permission
                if (!await HasFolderPermissionAsync(folderId, userId, userDepartmentId, PermissionType.View))
                {
                    throw new UnauthorizedAccessException("Access denied to this folder");
                }

                var userPermission = await GetUserEffectivePermissionAsync(folderId, userId, userDepartmentId);

                // ✅ FIXED: Calculate actual document count from database instead of using cached field
                var actualDocumentCount = await _unitOfWork.GetRepository<DocumentVersion>()
                    .CountAsync(predicate: dv => dv.FolderId == folder.Id && dv.DeletedTime == null);

                // Map permissions and enrich them with names
                var permissionResponses = folder.FolderPermissions.Select(MapToFolderPermissionResponse).ToList();
                var enrichedPermissions = await _folderPermissionEnrichmentService.EnrichFolderPermissionResponsesAsync(permissionResponses);

                _logger.LogDebug("Folder '{FolderName}' detail: {ActualDocumentCount} documents (cached: {CachedDocumentCount})",
                    folder.Name, actualDocumentCount, folder.DocumentCount);

                return new FolderDetailResponse
                {
                    Id = folder.Id,
                    Name = folder.Name,
                    Description = folder.Description,
                    DepartmentId = folder.DepartmentId,
                    ParentFolderId = folder.ParentFolderId,
                    GoogleDriveFolderId = folder.GoogleDriveFolderId,
                    IsSystemFolder = folder.IsSystemFolder,
                    IsPublic = folder.IsPublic,
                    Level = folder.Level,
                    FullPath = folder.FullPath,
                    FolderType = folder.FolderType,
                    SubFolderCount = folder.SubFolderCount,
                    DocumentCount = actualDocumentCount, // ✅ Use actual count instead of cached field
                    CanBeDeleted = folder.CanBeDeleted,
                    UserPermission = userPermission,
                    CanCreateSubfolders = userPermission?.Includes(PermissionType.Edit) == true,
                    CanUploadDocuments = userPermission?.Includes(PermissionType.Edit) == true,
                    CanManagePermissions = userPermission?.Includes(PermissionType.Manage) == true,
                    ParentFolder = folder.ParentFolder != null ? await MapToFolderSummaryAsync(folder.ParentFolder, userPermission) : null,
                    SubFolders = folder.SubFolders.Select(sf => MapToFolderSummary(sf, userPermission)).ToList(),
                    Permissions = enrichedPermissions,
                    CreatedTime = folder.CreatedTime,
                    LastUpdatedTime = folder.LastUpdatedTime,
                    CreatedBy = folder.CreatedBy,
                    LastUpdatedBy = folder.LastUpdatedBy
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder details for {FolderId}", folderId);
                throw;
            }
        }

        public async Task<FolderDetailResponse> GetFolderByPathAsync(string fullPath, string? departmentId = null)
        {
            try
            {
                _logger.LogInformation("Getting folder by path {FullPath} for department {DepartmentId}", fullPath, departmentId);

                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(
                        predicate: f => f.FullPath == fullPath && 
                                       (departmentId == null ? f.IsPublic : f.DepartmentId == departmentId) && 
                                       !f.IsDeleted
                    );

                if (folder == null)
                {
                    throw new KeyNotFoundException($"Folder with path {fullPath} not found");
                }

                return await GetFolderByIdAsync(folder.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting folder by path {FullPath}", fullPath);
                throw;
            }
        }

        public async Task<FolderDetailResponse> CreateFolderAsync(CreateFolderRequest request)
        {
            string? googleDriveFolderId = null;
            Folder? createdFolder = null;
            bool parentFolderUpdated = false;

            try
            {
                _logger.LogInformation("Creating folder {FolderName} in parent {ParentId}", request.Name, request.ParentFolderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // Validate folder name
                var validation = await ValidateFolderNameAsync(request.Name, request.ParentFolderId);
                if (!validation.IsValid)
                {
                    throw new ArgumentException($"Invalid folder name: {string.Join(", ", validation.Errors)}");
                }

                // Determine department and check permissions
                var targetDepartmentId = request.DepartmentId ?? userDepartmentId;
                if (request.IsPublic)
                {
                    targetDepartmentId = null; // Public folders don't belong to a department
                }

                // ✅ DEPARTMENT BOUNDARY VALIDATION: Prevent operations outside department
                await ValidateDepartmentBoundaryAsync(request.ParentFolderId, userId, userDepartmentId, "create folders");

                // Check if user can create folders in the target location
                if (request.ParentFolderId != null)
                {
                    if (!await HasFolderPermissionAsync(request.ParentFolderId, userId, userDepartmentId, PermissionType.Edit))
                    {
                        throw new UnauthorizedAccessException("Access denied to create folders in this location");
                    }
                }
                else if (!request.IsPublic && targetDepartmentId != userDepartmentId && !await IsUserAdminOrManagerAsync(userId))
                {
                    throw new UnauthorizedAccessException("Access denied to create root folders in other departments");
                }

                // Get parent folder info for path building
                Folder? parentFolder = null;
                string parentPath = "";
                int level = 0;

                if (request.ParentFolderId != null)
                {
                    parentFolder = await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(predicate: f => f.Id == request.ParentFolderId && !f.IsDeleted);

                    if (parentFolder == null)
                    {
                        throw new ArgumentException("Parent folder not found");
                    }

                    parentPath = parentFolder.FullPath;
                    level = parentFolder.Level + 1;

                    // Check maximum depth
                    if (level > FolderConstant.Validation.MaxFolderDepth)
                    {
                        throw new ArgumentException($"Maximum folder depth ({FolderConstant.Validation.MaxFolderDepth}) exceeded");
                    }
                }

                // Step 1: Create folder in Google Drive
                googleDriveFolderId = await _googleDriveService.CreateFolderAsync(
                    request.Name,
                    parentFolder?.GoogleDriveFolderId,
                    request.Description
                );

                _logger.LogDebug("Created Google Drive folder {GoogleDriveFolderId} for '{FolderName}'", googleDriveFolderId, request.Name);

                // Step 2: Create folder entity in database
                var folder = new Folder
                {
                    Name = request.Name,
                    Description = request.Description,
                    DepartmentId = targetDepartmentId ?? string.Empty,
                    ParentFolderId = request.ParentFolderId,
                    GoogleDriveFolderId = googleDriveFolderId,
                    IsSystemFolder = false,
                    IsPublic = request.IsPublic,
                    Level = level,
                    FullPath = FolderConstant.Helpers.BuildFullPath(parentPath, request.Name),
                    CreatedBy = userId,
                    CreatedTime = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<Folder>().InsertAsync(folder);
                createdFolder = folder;

                // Step 3: Set initial permissions
                if (request.InitialPermissions?.Any() == true)
                {
                    foreach (var permission in request.InitialPermissions)
                    {
                        var folderPermission = new FolderPermission
                        {
                            FolderId = folder.Id,
                            UserId = permission.UserId,
                            DepartmentId = permission.DepartmentId,
                            PermissionType = permission.PermissionType,
                            ExpiresAt = permission.ExpiresAt,
                            IsActive = true,
                            CreatedBy = userId,
                            CreatedTime = DateTime.UtcNow
                        };

                        await _unitOfWork.GetRepository<FolderPermission>().InsertAsync(folderPermission);
                    }
                }

                // Step 4: Update parent folder counts
                if (parentFolder != null)
                {
                    parentFolder.SubFolderCount++;
                    await _unitOfWork.GetRepository<Folder>().UpdateAsync(parentFolder);
                    parentFolderUpdated = true;
                }

                // Step 5: Commit all database changes
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully created folder {FolderId} ({FolderName}) with Google Drive ID {GoogleDriveFolderId}",
                    folder.Id, folder.Name, googleDriveFolderId);

                return await GetFolderByIdAsync(folder.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating folder {FolderName}. Initiating rollback.", request.Name);

                // Rollback: Delete Google Drive folder if it was created
                if (!string.IsNullOrEmpty(googleDriveFolderId))
                {
                    try
                    {
                        await _googleDriveService.DeleteFolderAsync(googleDriveFolderId);
                        _logger.LogInformation("Successfully rolled back Google Drive folder {GoogleDriveFolderId}", googleDriveFolderId);
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "Failed to rollback Google Drive folder {GoogleDriveFolderId}. Manual cleanup may be required.", googleDriveFolderId);
                    }
                }

                throw;
            }
        }

        public async Task<FolderDetailResponse> UpdateFolderAsync(string folderId, UpdateFolderRequest request)
        {
            try
            {
                _logger.LogInformation("Updating folder {FolderId}", folderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // Check permission
                if (!await HasFolderPermissionAsync(folderId, userId, userDepartmentId, PermissionType.Edit))
                {
                    throw new UnauthorizedAccessException("Access denied to update this folder");
                }

                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(predicate: f => f.Id == folderId && !f.IsDeleted);

                if (folder == null)
                {
                    throw new KeyNotFoundException($"Folder with ID {folderId} not found");
                }

                if (folder.IsSystemFolder)
                {
                    throw new InvalidOperationException("System folders cannot be modified");
                }

                bool hasChanges = false;

                // Update name if provided
                if (!string.IsNullOrEmpty(request.Name) && request.Name != folder.Name)
                {
                    // Validate new name
                    var validation = await ValidateFolderNameAsync(request.Name, folder.ParentFolderId);
                    if (!validation.IsValid)
                    {
                        throw new ArgumentException($"Invalid folder name: {string.Join(", ", validation.Errors)}");
                    }

                    folder.Name = request.Name;
                    hasChanges = true;

                    // Update full path and all child paths
                    await UpdateFolderPathsAsync(folder);
                }

                // Update description if provided
                if (request.Description != null && request.Description != folder.Description)
                {
                    folder.Description = request.Description;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    folder.LastUpdatedBy = userId;
                    folder.LastUpdatedTime = DateTime.UtcNow;

                    await _unitOfWork.GetRepository<Folder>().UpdateAsync(folder);

                    // Update Google Drive folder
                    await _googleDriveService.UpdateFolderAsync(folder.GoogleDriveFolderId, request.Name, request.Description);

                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Successfully updated folder {FolderId}", folderId);
                }

                return await GetFolderByIdAsync(folderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<FolderDetailResponse> MoveFolderAsync(string folderId, MoveFolderRequest request)
        {
            // Note: Transaction management is handled by UnitOfWork CommitAsync
            try
            {
                _logger.LogInformation("Moving folder {FolderId} to parent {NewParentId}", folderId, request.NewParentFolderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // ✅ DEPARTMENT BOUNDARY VALIDATION: Prevent moving folders outside department
                await ValidateDepartmentBoundaryAsync(folderId, userId, userDepartmentId, "move folders from");
                await ValidateDepartmentBoundaryAsync(request.NewParentFolderId, userId, userDepartmentId, "move folders to");

                // Check permission on source folder
                if (!await HasFolderPermissionAsync(folderId, userId, userDepartmentId, PermissionType.Manage))
                {
                    throw new UnauthorizedAccessException("Access denied to move this folder");
                }

                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(
                        predicate: f => f.Id == folderId && !f.IsDeleted,
                        include: i => i.Include(f => f.ParentFolder)
                    );

                if (folder == null)
                {
                    throw new KeyNotFoundException($"Folder with ID {folderId} not found");
                }

                if (folder.IsSystemFolder)
                {
                    throw new InvalidOperationException("System folders cannot be moved");
                }

                // Check permission on target parent folder
                if (request.NewParentFolderId != null)
                {
                    if (!await HasFolderPermissionAsync(request.NewParentFolderId, userId, userDepartmentId, PermissionType.Edit))
                    {
                        throw new UnauthorizedAccessException("Access denied to move folder to this location");
                    }

                    // Check for circular reference
                    if (await IsDescendantFolderAsync(request.NewParentFolderId, folderId))
                    {
                        throw new ArgumentException("Cannot move folder to its own descendant");
                    }
                }

                var oldParentId = folder.ParentFolderId;
                var newParentFolder = request.NewParentFolderId != null
                    ? await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(predicate: f => f.Id == request.NewParentFolderId && !f.IsDeleted)
                    : null;

                // Store old path for document updates
                var oldFolderPath = folder.FullPath;

                // Update folder hierarchy
                folder.ParentFolderId = request.NewParentFolderId;
                folder.Level = newParentFolder?.Level + 1 ?? 0;
                folder.LastUpdatedBy = userId;
                folder.LastUpdatedTime = DateTime.UtcNow;

                // Update paths
                await UpdateFolderPathsAsync(folder);

                // ✅ CRITICAL FIX: Update documents in moved folder and subfolders
                await UpdateDocumentsInMovedFolderAsync(folderId, oldFolderPath, folder.FullPath);

                // Move in Google Drive
                var googleDriveSuccess = await _googleDriveService.MoveFolderAsync(folder.GoogleDriveFolderId, newParentFolder?.GoogleDriveFolderId);
                if (!googleDriveSuccess)
                {
                    throw new InvalidOperationException("Failed to move folder in Google Drive");
                }

                // Update folder counts
                if (oldParentId != null)
                {
                    var oldParent = await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(predicate: f => f.Id == oldParentId);
                    if (oldParent != null)
                    {
                        oldParent.SubFolderCount--;
                        await _unitOfWork.GetRepository<Folder>().UpdateAsync(oldParent);
                    }
                }

                if (newParentFolder != null)
                {
                    newParentFolder.SubFolderCount++;
                    await _unitOfWork.GetRepository<Folder>().UpdateAsync(newParentFolder);
                }

                await _unitOfWork.GetRepository<Folder>().UpdateAsync(folder);

                // Handle permissions
                if (!request.PreservePermissions && newParentFolder != null)
                {
                    await InheritPermissionsFromParentAsync(folderId, request.NewParentFolderId);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully moved folder {FolderId} to parent {NewParentId}", folderId, request.NewParentFolderId);

                return await GetFolderByIdAsync(folderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<bool> DeleteFolderAsync(string folderId, bool force = false)
        {
            try
            {
                _logger.LogInformation("Deleting folder {FolderId} with force={Force}", folderId, force);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // Get folder first to check if it exists
                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(
                        predicate: f => f.Id == folderId && !f.IsDeleted,
                        include: i => i.Include(f => f.SubFolders.Where(sf => !sf.IsDeleted))
                                      .Include(f => f.Documents.Where(d => !string.IsNullOrEmpty(d.FolderId)))
                    );

                if (folder == null)
                {
                    var message = string.Format(FolderMessageConstant.System.FolderNotFound, folderId);
                    _logger.LogWarning(message);
                    throw new KeyNotFoundException(message);
                }

                // Check permission
                if (!await HasFolderPermissionAsync(folderId, userId, userDepartmentId, PermissionType.Delete))
                {
                    var message = string.Format(FolderMessageConstant.Permissions.CannotDeleteFolder, folder.Name);
                    _logger.LogWarning("User {UserId} attempted to delete folder {FolderId} without permission", userId, folderId);
                    throw new UnauthorizedAccessException(message);
                }

                if (folder.IsSystemFolder)
                {
                    var message = string.Format(FolderMessageConstant.Permissions.CannotDeleteSystemFolder, folder.Name);
                    _logger.LogWarning("Attempted to delete system folder {FolderId} ({FolderName})", folderId, folder.Name);
                    throw new InvalidOperationException(message);
                }

                // Check if folder can be deleted
                if (!force && (!folder.CanBeDeleted || folder.SubFolders.Any() || folder.Documents.Any()))
                {
                    var subfolderCount = folder.SubFolders.Count;
                    var documentCount = folder.Documents.Count;

                    string message;
                    if (subfolderCount > 0 && documentCount > 0)
                    {
                        message = string.Format(FolderMessageConstant.Content.FolderContainsBoth, folder.Name, documentCount, subfolderCount);
                    }
                    else if (documentCount > 0)
                    {
                        message = string.Format(FolderMessageConstant.Content.FolderContainsDocuments, folder.Name, documentCount);
                    }
                    else if (subfolderCount > 0)
                    {
                        message = string.Format(FolderMessageConstant.Content.FolderContainsSubfolders, folder.Name, subfolderCount);
                    }
                    else
                    {
                        message = string.Format(FolderMessageConstant.Content.FolderNotEmpty, folder.Name, subfolderCount + documentCount);
                    }

                    message += " " + FolderMessageConstant.Content.MustDeleteContentsFirst;

                    _logger.LogWarning("Cannot delete folder {FolderId}: contains {DocumentCount} documents and {SubfolderCount} subfolders",
                        folderId, documentCount, subfolderCount);
                    throw new InvalidOperationException(message);
                }

                // Soft delete
                folder.IsDeleted = true;
                folder.DeletedBy = userId;
                folder.DeletedTime = DateTime.UtcNow;

                await _unitOfWork.GetRepository<Folder>().UpdateAsync(folder);

                // Update parent folder count
                if (folder.ParentFolderId != null)
                {
                    var parentFolder = await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(predicate: f => f.Id == folder.ParentFolderId);
                    if (parentFolder != null)
                    {
                        parentFolder.SubFolderCount--;
                        await _unitOfWork.GetRepository<Folder>().UpdateAsync(parentFolder);
                    }
                }

                // Delete from Google Drive if force delete
                if (force && !string.IsNullOrEmpty(folder.GoogleDriveFolderId))
                {
                    try
                    {
                        await _googleDriveService.DeleteFolderAsync(folder.GoogleDriveFolderId);
                        _logger.LogInformation(FolderMessageConstant.GoogleDriveSync.FolderDeletedFromGoogleDrive, folder.Name);
                    }
                    catch (Exception gdEx)
                    {
                        _logger.LogWarning(gdEx, "Failed to delete folder from Google Drive {GoogleDriveFolderId}, but database deletion will proceed",
                            folder.GoogleDriveFolderId);
                    }
                }

                await _unitOfWork.CommitAsync();

                var successMessage = string.Format(FolderMessageConstant.Operations.FolderDeletedSuccessfully, folder.Name);
                _logger.LogInformation(successMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<List<FolderSummaryResponse>> GetAccessibleFoldersAsync(string userId, string departmentId, PermissionType permissionType = PermissionType.View)
        {
            try
            {
                _logger.LogInformation("Getting accessible folders for user {UserId} with permission {Permission}", userId, permissionType);

                // Get folders user has direct access to
                var accessibleFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(
                        predicate: f => !f.IsDeleted &&
                                       (f.IsPublic || f.DepartmentId == departmentId ||
                                        f.FolderPermissions.Any(fp => fp.IsActive &&
                                                                     ((fp.UserId == userId) ||
                                                                      (fp.DepartmentId == departmentId)) &&
                                                                     fp.PermissionType >= permissionType)),
                        include: i => i.Include(f => f.FolderPermissions)
                    );

                var result = new List<FolderSummaryResponse>();

                foreach (var folder in accessibleFolders)
                {
                    var userPermission = await GetUserEffectivePermissionAsync(folder.Id, userId, departmentId);
                    if (userPermission?.Includes(permissionType) == true)
                    {
                        result.Add(MapToFolderSummary(folder, userPermission));
                    }
                }

                return result.OrderBy(f => f.FullPath).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accessible folders for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// ✅ SIMPLIFIED: Simple department-based permission system
        /// - All department members can VIEW their department's folders/files
        /// - Only specific users get EDIT permissions (managed by managers)
        /// </summary>
        public async Task<bool> HasFolderPermissionAsync(string folderId, string userId, string departmentId, PermissionType requiredPermission)
        {
            try
            {
                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(
                        predicate: f => f.Id == folderId && !f.IsDeleted,
                        include: i => i.Include(f => f.FolderPermissions.Where(fp => fp.IsActive))
                    );

                if (folder == null)
                {
                    return false;
                }

                // ✅ 1. PUBLIC FOLDERS: Everyone can view
                if (folder.IsPublic && requiredPermission == PermissionType.View)
                {
                    return true;
                }

                // ✅ 2. DEPARTMENT FOLDERS: Simple department-based access
                if (folder.DepartmentId == departmentId)
                {
                    // All department members can VIEW
                    if (requiredPermission == PermissionType.View)
                    {
                        return true;
                    }

                    // Managers have full control
                    if (await IsUserManagerAsync(userId))
                    {
                        return true; // Managers can do everything
                    }

                    // For EDIT/DELETE/MANAGE: Check if user has explicit permission
                    var userPermission = await GetUserExplicitPermissionAsync(folderId, userId);
                    return userPermission?.Includes(requiredPermission) == true;
                }

                // ✅ 3. OTHER DEPARTMENTS: No access unless explicitly granted
                var explicitPermission = await GetUserExplicitPermissionAsync(folderId, userId);
                return explicitPermission?.Includes(requiredPermission) == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking folder permission for user {UserId} on folder {FolderId}", userId, folderId);
                return false;
            }
        }

        public async Task<List<FolderPermissionResponse>> GetFolderPermissionsAsync(string folderId)
        {
            try
            {
                _logger.LogInformation("Getting permissions for folder {FolderId}", folderId);

                var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(
                        predicate: fp => fp.FolderId == folderId && fp.IsActive,
                        include: i => i.Include(fp => fp.Folder)
                    );

                var permissionResponses = permissions.Select(MapToFolderPermissionResponse).ToList();

                // Enrich the responses with user names, emails, and department names
                var enrichedPermissions = await _folderPermissionEnrichmentService.EnrichFolderPermissionResponsesAsync(permissionResponses);

                _logger.LogInformation("Retrieved and enriched {Count} permissions for folder {FolderId}", enrichedPermissions.Count, folderId);
                return enrichedPermissions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permissions for folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<FolderPermissionResponse> SetFolderPermissionAsync(string folderId, SetFolderPermissionRequest request)
        {
            try
            {
                _logger.LogInformation("Setting permission for folder {FolderId}", folderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // Check if user can manage permissions
                if (!await HasFolderPermissionAsync(folderId, userId, userDepartmentId, PermissionType.Manage))
                {
                    throw new UnauthorizedAccessException("Access denied to manage folder permissions");
                }

                // Validate request
                if (string.IsNullOrEmpty(request.UserId) && string.IsNullOrEmpty(request.DepartmentId))
                {
                    throw new ArgumentException("Either UserId or DepartmentId must be provided");
                }

                if (!string.IsNullOrEmpty(request.UserId) && !string.IsNullOrEmpty(request.DepartmentId))
                {
                    throw new ArgumentException("Cannot set both UserId and DepartmentId");
                }

                // Check if permission already exists
                var existingPermission = await _unitOfWork.GetRepository<FolderPermission>()
                    .SingleOrDefaultAsync(
                        predicate: fp => fp.FolderId == folderId && fp.IsActive &&
                                        fp.UserId == request.UserId && fp.DepartmentId == request.DepartmentId
                    );

                if (existingPermission != null)
                {
                    // Update existing permission
                    existingPermission.PermissionType = request.PermissionType;
                    existingPermission.IsDenied = request.IsDenied;
                    existingPermission.ExpiresAt = request.ExpiresAt;
                    existingPermission.LastUpdatedBy = userId;
                    existingPermission.LastUpdatedTime = DateTime.UtcNow;

                    await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(existingPermission);
                }
                else
                {
                    // Create new permission
                    var newPermission = new FolderPermission
                    {
                        FolderId = folderId,
                        UserId = request.UserId,
                        DepartmentId = request.DepartmentId,
                        PermissionType = request.PermissionType,
                        IsDenied = request.IsDenied,
                        ExpiresAt = request.ExpiresAt,
                        IsActive = true,
                        CreatedBy = userId,
                        CreatedTime = DateTime.UtcNow
                    };

                    await _unitOfWork.GetRepository<FolderPermission>().InsertAsync(newPermission);
                    existingPermission = newPermission;
                }

                // Apply to subfolders if requested
                if (request.ApplyToSubfolders)
                {
                    await ApplyPermissionToSubfoldersAsync(folderId, request, userId);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully set permission for folder {FolderId}", folderId);

                // Map and enrich the permission response
                var permissionResponse = MapToFolderPermissionResponse(existingPermission);
                var enrichedPermission = await _folderPermissionEnrichmentService.EnrichFolderPermissionResponseAsync(permissionResponse);

                return enrichedPermission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting permission for folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<bool> RemoveFolderPermissionAsync(string folderId, string permissionId)
        {
            try
            {
                _logger.LogInformation("Removing permission {PermissionId} from folder {FolderId}", permissionId, folderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                // Check if user can manage permissions
                if (!await HasFolderPermissionAsync(folderId, userId, userDepartmentId, PermissionType.Manage))
                {
                    throw new UnauthorizedAccessException("Access denied to manage folder permissions");
                }

                var permission = await _unitOfWork.GetRepository<FolderPermission>()
                    .SingleOrDefaultAsync(predicate: fp => fp.Id == permissionId && fp.FolderId == folderId);

                if (permission == null)
                {
                    throw new KeyNotFoundException($"Permission with ID {permissionId} not found");
                }

                // Soft delete permission
                permission.IsActive = false;
                permission.LastUpdatedBy = userId;
                permission.LastUpdatedTime = DateTime.UtcNow;

                await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(permission);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully removed permission {PermissionId} from folder {FolderId}", permissionId, folderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing permission {PermissionId} from folder {FolderId}", permissionId, folderId);
                throw;
            }
        }

        public async Task<List<string>> InitializeDepartmentFoldersAsync(string departmentId, string departmentName)
        {
            try
            {
                _logger.LogInformation("Initializing folders for department {DepartmentId} ({DepartmentName})", departmentId, departmentName);

                var createdFolderIds = new List<string>();

                // Check if department folders already exist
                var existingFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(predicate: f => f.DepartmentId == departmentId && !f.IsDeleted);

                if (existingFolders.Any())
                {
                    _logger.LogInformation("Department folders already exist for {DepartmentId}", departmentId);
                    return existingFolders.Select(f => f.Id).ToList();
                }

                // Initialize Google Drive folder hierarchy
                var googleDriveFolders = await _googleDriveService.InitializeDepartmentFolderHierarchyAsync(departmentId, departmentName);

                // Create department root folder
                var departmentFolder = new Folder
                {
                    Name = departmentName,
                    Description = $"Root folder for {departmentName} department",
                    DepartmentId = departmentId,
                    ParentFolderId = null,
                    GoogleDriveFolderId = googleDriveFolders["Department"],
                    IsSystemFolder = false,
                    IsPublic = false,
                    Level = 0,
                    FullPath = departmentName,
                    CreatedBy = "System",
                    CreatedTime = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<Folder>().InsertAsync(departmentFolder);

                // Save the department folder first to get its ID
                await _unitOfWork.CommitAsync();
                createdFolderIds.Add(departmentFolder.Id);

                // Create system folders (only drafts for temporary storage)
                var systemFolders = new[]
                {
                    new { Name = FolderConstant.SystemFolders.Draft, Type = FolderType.Draft }
                };

                // No functional folders created automatically
                // Managers can create custom folders as needed using the folder management APIs

                // Create system folders
                foreach (var systemFolder in systemFolders)
                {
                    var folder = new Folder
                    {
                        Name = systemFolder.Name,
                        Description = $"System folder for {systemFolder.Type} documents",
                        DepartmentId = departmentId,
                        ParentFolderId = departmentFolder.Id,
                        GoogleDriveFolderId = googleDriveFolders[FolderConstant.SystemFolders.Draft],
                        IsSystemFolder = true,
                        IsPublic = false,
                        Level = 1,
                        FullPath = FolderConstant.Helpers.BuildFullPath(departmentFolder.FullPath, systemFolder.Name),
                        FolderType = systemFolder.Type,
                        CreatedBy = "System",
                        CreatedTime = DateTime.UtcNow
                    };

                    await _unitOfWork.GetRepository<Folder>().InsertAsync(folder);
                    createdFolderIds.Add(folder.Id);
                }

                // Update department folder subfolder count (only system folders)
                departmentFolder.SubFolderCount = systemFolders.Length;
                await _unitOfWork.GetRepository<Folder>().UpdateAsync(departmentFolder);

                // Final commit for system folders and department folder update
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully initialized {Count} folders for department {DepartmentId}", createdFolderIds.Count, departmentId);
                return createdFolderIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing folders for department {DepartmentId}", departmentId);
                throw;
            }
        }

        public async Task<List<string>> InitializePublicFoldersAsync()
        {
            try
            {
                _logger.LogInformation("Initializing public folders");

                var createdFolderIds = new List<string>();

                // Check if public folders already exist
                var existingFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(predicate: f => f.IsPublic && !f.IsDeleted);

                if (existingFolders.Any())
                {
                    _logger.LogInformation("Public folders already exist");
                    return existingFolders.Select(f => f.Id).ToList();
                }

                // Initialize Google Drive folder hierarchy
                var googleDriveFolders = await _googleDriveService.InitializePublicFolderHierarchyAsync();

                // Create public root folder
                var publicFolder = new Folder
                {
                    Name = FolderConstant.RootFolders.Public,
                    Description = "Public documents accessible to all employees",
                    DepartmentId = string.Empty,
                    ParentFolderId = null,
                    GoogleDriveFolderId = googleDriveFolders["Public"],
                    IsSystemFolder = false,
                    IsPublic = true,
                    Level = 0,
                    FullPath = FolderConstant.RootFolders.Public,
                    CreatedBy = "System",
                    CreatedTime = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<Folder>().InsertAsync(publicFolder);

                // Save the public folder first to get its ID
                await _unitOfWork.CommitAsync();
                createdFolderIds.Add(publicFolder.Id);

                // Create system folders (only drafts for temporary storage)
                var systemFolders = new[]
                {
                    new { Name = FolderConstant.SystemFolders.Draft, Type = FolderType.Draft }
                };

                // No functional folders created automatically
                // Admins can create custom public folders as needed using the folder management APIs

                // Create system folders
                foreach (var systemFolder in systemFolders)
                {
                    var folder = new Folder
                    {
                        Name = systemFolder.Name,
                        Description = $"Public system folder for {systemFolder.Type} documents",
                        DepartmentId = string.Empty,
                        ParentFolderId = publicFolder.Id,
                        GoogleDriveFolderId = googleDriveFolders[FolderConstant.SystemFolders.Draft],
                        IsSystemFolder = true,
                        IsPublic = true,
                        Level = 1,
                        FullPath = FolderConstant.Helpers.BuildFullPath(publicFolder.FullPath, systemFolder.Name),
                        FolderType = systemFolder.Type,
                        CreatedBy = "System",
                        CreatedTime = DateTime.UtcNow
                    };

                    await _unitOfWork.GetRepository<Folder>().InsertAsync(folder);
                    createdFolderIds.Add(folder.Id);
                }

                // Update public folder subfolder count (only system folders)
                publicFolder.SubFolderCount = systemFolders.Length;
                await _unitOfWork.GetRepository<Folder>().UpdateAsync(publicFolder);

                // Final commit for system folders and public folder update
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully initialized {Count} public folders", createdFolderIds.Count);
                return createdFolderIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing public folders");
                throw;
            }
        }

        public async Task<List<FolderBreadcrumbResponse>> GetFolderBreadcrumbAsync(string folderId)
        {
            try
            {
                var breadcrumbs = new List<FolderBreadcrumbResponse>();
                var currentFolderId = folderId;

                while (!string.IsNullOrEmpty(currentFolderId))
                {
                    var folder = await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(predicate: f => f.Id == currentFolderId && !f.IsDeleted);

                    if (folder == null) break;

                    breadcrumbs.Insert(0, new FolderBreadcrumbResponse
                    {
                        Id = folder.Id,
                        Name = folder.Name,
                        Level = folder.Level,
                        IsSystemFolder = folder.IsSystemFolder,
                        IsCurrent = folder.Id == folderId
                    });

                    currentFolderId = folder.ParentFolderId;
                }

                return breadcrumbs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting breadcrumb for folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<List<FolderSummaryResponse>> SearchFoldersAsync(string searchTerm, string? departmentId, string userId)
        {
            try
            {
                var userDepartmentId = JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor);

                var folders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(
                        predicate: f => !f.IsDeleted &&
                                       (f.Name.Contains(searchTerm) || f.FullPath.Contains(searchTerm)) &&
                                       (departmentId == null ? f.IsPublic : f.DepartmentId == departmentId),
                        include: i => i.Include(f => f.FolderPermissions.Where(fp => fp.IsActive))
                    );

                var result = new List<FolderSummaryResponse>();

                foreach (var folder in folders)
                {
                    if (await HasFolderPermissionAsync(folder.Id, userId, userDepartmentId, PermissionType.View))
                    {
                        var userPermission = await GetUserEffectivePermissionAsync(folder.Id, userId, userDepartmentId);
                        result.Add(MapToFolderSummary(folder, userPermission));
                    }
                }

                return result.OrderBy(f => f.FullPath).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching folders with term '{SearchTerm}'", searchTerm);
                throw;
            }
        }

        public async Task<FolderValidationResult> ValidateFolderNameAsync(string folderName, string? parentFolderId)
        {
            var result = new FolderValidationResult();

            try
            {
                // Basic validation
                if (!FolderConstant.Helpers.IsValidFolderName(folderName))
                {
                    result.Errors.Add("Invalid folder name. Check length and special characters.");
                }

                // Check for system folder prefix
                if (FolderConstant.Helpers.IsSystemFolder(folderName))
                {
                    result.Errors.Add("Folder names cannot start with underscore (reserved for system folders).");
                }

                // Check for uniqueness within parent
                if (parentFolderId != null)
                {
                    var existingFolder = await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(
                            predicate: f => f.ParentFolderId == parentFolderId &&
                                           f.Name == folderName &&
                                           !f.IsDeleted
                        );

                    if (existingFolder != null)
                    {
                        result.Errors.Add("A folder with this name already exists in the parent folder.");
                    }
                }

                result.IsValid = !result.Errors.Any();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating folder name '{FolderName}'", folderName);
                result.Errors.Add("An error occurred during validation.");
                result.IsValid = false;
                return result;
            }
        }

        #region Helper Methods

        private async Task<FolderNodeResponse> BuildFolderNodeAsync(Folder folder, string userId, string userDepartmentId, bool includeSystemFolders, int? maxDepth, int currentDepth)
        {
            var userPermission = await GetUserEffectivePermissionAsync(folder.Id, userId, userDepartmentId);

            // ✅ FIXED: Calculate actual document count from database instead of using cached field
            var actualDocumentCount = await _unitOfWork.GetRepository<DocumentVersion>()
                .CountAsync(predicate: dv => dv.FolderId == folder.Id && dv.DeletedTime == null);

            var node = new FolderNodeResponse
            {
                Id = folder.Id,
                Name = folder.Name,
                Description = folder.Description,
                FullPath = folder.FullPath,
                Level = folder.Level,
                IsSystemFolder = folder.IsSystemFolder,
                IsPublic = folder.IsPublic,
                FolderType = folder.FolderType?.ToString(),
                SubFolderCount = folder.SubFolderCount,
                DocumentCount = actualDocumentCount, // ✅ Use actual count instead of cached field
                UserPermission = userPermission?.ToString(),
                CanCreateSubfolders = userPermission?.Includes(PermissionType.Edit) == true,
                CanUploadDocuments = userPermission?.Includes(PermissionType.Edit) == true,
                CreatedTime = folder.CreatedTime,
                LastUpdatedTime = folder.LastUpdatedTime,
                CreatedBy = folder.CreatedBy
            };

            _logger.LogDebug("Folder '{FolderName}' has {ActualDocumentCount} documents (cached: {CachedDocumentCount})",
                folder.Name, actualDocumentCount, folder.DocumentCount);

            // Load subfolders if within depth limit
            if (maxDepth == null || currentDepth < maxDepth)
            {
                var subFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(
                        predicate: f => f.ParentFolderId == folder.Id &&
                                       !f.IsDeleted &&
                                       (includeSystemFolders || !f.IsSystemFolder),
                        include: i => i.Include(f => f.SubFolders.Where(sf => !sf.IsDeleted))
                                      .Include(f => f.Documents.Where(d => !string.IsNullOrEmpty(d.FolderId)))
                    );

                foreach (var subFolder in subFolders)
                {
                    if (await HasFolderPermissionAsync(subFolder.Id, userId, userDepartmentId, PermissionType.View))
                    {
                        var subNode = await BuildFolderNodeAsync(subFolder, userId, userDepartmentId, includeSystemFolders, maxDepth, currentDepth + 1);
                        node.SubFolders.Add(subNode);
                    }
                }
            }

            return node;
        }

        /// <summary>
        /// ✅ NEW: Get only explicit permissions (no defaults)
        /// Used for checking EDIT/DELETE/MANAGE permissions for non-managers
        /// </summary>
        private async Task<PermissionType?> GetUserExplicitPermissionAsync(string folderId, string userId)
        {
            var folder = await _unitOfWork.GetRepository<Folder>()
                .SingleOrDefaultAsync(
                    predicate: f => f.Id == folderId,
                    include: i => i.Include(f => f.FolderPermissions.Where(fp => fp.IsActive))
                );

            if (folder == null) return null;

            // Only check explicit user permission (no defaults)
            var userPermission = folder.FolderPermissions
                .Where(fp => fp.UserId == userId && fp.IsValid && !fp.IsDenied)
                .OrderByDescending(fp => fp.PermissionType)
                .FirstOrDefault();

            return userPermission?.PermissionType;
        }

        /// <summary>
        /// ✅ UPDATED: Get effective permission including defaults
        /// Used for display purposes and general permission queries
        /// </summary>
        private async Task<PermissionType?> GetUserEffectivePermissionAsync(string folderId, string userId, string userDepartmentId)
        {
            var folder = await _unitOfWork.GetRepository<Folder>()
                .SingleOrDefaultAsync(
                    predicate: f => f.Id == folderId,
                    include: i => i.Include(f => f.FolderPermissions.Where(fp => fp.IsActive))
                );

            if (folder == null) return null;

            // 1. Check explicit user permission first
            var userPermission = folder.FolderPermissions
                .Where(fp => fp.UserId == userId && fp.IsValid)
                .OrderByDescending(fp => fp.PermissionType)
                .FirstOrDefault();

            if (userPermission != null)
            {
                return userPermission.IsDenied ? null : userPermission.PermissionType;
            }

            // 2. Default permissions based on simple department system
            if (folder.IsPublic)
            {
                return FolderConstant.DefaultPermissions.PublicFolderPermission; // View
            }

            if (folder.DepartmentId == userDepartmentId)
            {
                return await IsUserManagerAsync(userId)
                    ? FolderConstant.DefaultPermissions.DepartmentManagerPermission // Manage
                    : FolderConstant.DefaultPermissions.DepartmentMemberPermission; // View
            }

            return null; // No access to other departments
        }

        private async Task<FolderSummaryResponse> MapToFolderSummaryAsync(Folder folder, PermissionType? userPermission)
        {
            // ✅ FIXED: Calculate actual document count from database instead of using cached field
            var actualDocumentCount = await _unitOfWork.GetRepository<DocumentVersion>()
                .CountAsync(predicate: dv => dv.FolderId == folder.Id && dv.DeletedTime == null);

            return new FolderSummaryResponse
            {
                Id = folder.Id,
                Name = folder.Name,
                Description = folder.Description,
                FullPath = folder.FullPath,
                Level = folder.Level,
                IsSystemFolder = folder.IsSystemFolder,
                IsPublic = folder.IsPublic,
                FolderType = folder.FolderType,
                SubFolderCount = folder.SubFolderCount,
                DocumentCount = actualDocumentCount, // ✅ Use actual count instead of cached field
                UserPermission = userPermission,
                CanCreateSubfolders = userPermission?.Includes(PermissionType.Edit) == true,
                CanUploadDocuments = userPermission?.Includes(PermissionType.Edit) == true,
                CreatedTime = folder.CreatedTime,
                LastUpdatedTime = folder.LastUpdatedTime,
                DepartmentId = folder.DepartmentId,
                CreatedBy = folder.CreatedBy
            };
        }

        // Keep the synchronous version for backward compatibility where async is not possible
        private FolderSummaryResponse MapToFolderSummary(Folder folder, PermissionType? userPermission)
        {
            return new FolderSummaryResponse
            {
                Id = folder.Id,
                Name = folder.Name,
                Description = folder.Description,
                FullPath = folder.FullPath,
                Level = folder.Level,
                IsSystemFolder = folder.IsSystemFolder,
                IsPublic = folder.IsPublic,
                FolderType = folder.FolderType,
                SubFolderCount = folder.SubFolderCount,
                DocumentCount = folder.DocumentCount, // ⚠️ Using cached count (for backward compatibility)
                UserPermission = userPermission,
                CanCreateSubfolders = userPermission?.Includes(PermissionType.Edit) == true,
                CanUploadDocuments = userPermission?.Includes(PermissionType.Edit) == true,
                CreatedTime = folder.CreatedTime,
                LastUpdatedTime = folder.LastUpdatedTime,
                DepartmentId = folder.DepartmentId,
                CreatedBy = folder.CreatedBy
            };
        }

        private FolderPermissionResponse MapToFolderPermissionResponse(FolderPermission permission)
        {
            return new FolderPermissionResponse
            {
                Id = permission.Id,
                FolderId = permission.FolderId,
                UserId = permission.UserId,
                DepartmentId = permission.DepartmentId,
                PermissionType = permission.PermissionType,
                PermissionDescription = permission.PermissionType.GetDescription(),
                IsInherited = permission.IsInherited,
                IsDenied = permission.IsDenied,
                ExpiresAt = permission.ExpiresAt,
                IsActive = permission.IsActive,
                IsValid = permission.IsValid,
                PermissionSource = permission.IsInherited ? "Inherited" :
                                 !string.IsNullOrEmpty(permission.DepartmentId) ? "Department" : "Direct",
                CreatedTime = permission.CreatedTime,
                CreatedBy = permission.CreatedBy
            };
        }

        private async Task<bool> IsUserAdminOrManagerAsync(string userId)
        {
            var userRole = JwtTokenHelper.GetUserRole(_httpContextAccessor);
            return userRole == "Admin" || userRole == "Manager";
        }

        private async Task<bool> IsUserManagerAsync(string userId)
        {
            var userRole = JwtTokenHelper.GetUserRole(_httpContextAccessor);
            return userRole == "Manager";
        }

        /// <summary>
        /// ✅ DEPARTMENT BOUNDARY VALIDATION: Prevent managers from operating outside their department
        /// </summary>
        private async Task ValidateDepartmentBoundaryAsync(string? targetFolderId, string userId, string? userDepartmentId, string operation)
        {
            try
            {
                // Admins can operate anywhere
                if (await IsUserAdminOrManagerAsync(userId) && JwtTokenHelper.GetUserRole(_httpContextAccessor) == "Admin")
                {
                    return; // Admins have no restrictions
                }

                // If no target folder specified (root level operation), check department restrictions
                if (string.IsNullOrEmpty(targetFolderId))
                {
                    return; // Root level operations are handled by existing permission checks
                }

                // Get target folder and validate department boundary
                var targetFolder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(predicate: f => f.Id == targetFolderId && !f.IsDeleted);

                if (targetFolder == null)
                {
                    throw new KeyNotFoundException($"Target folder not found");
                }

                // Check if target folder is within user's department or public
                if (!targetFolder.IsPublic && targetFolder.DepartmentId != userDepartmentId)
                {
                    // Get department name for better error message
                    var targetDepartmentName = await GetDepartmentNameAsync(targetFolder.DepartmentId);
                    var userDepartmentName = await GetDepartmentNameAsync(userDepartmentId);

                    throw new UnauthorizedAccessException(
                        $"Access denied: Cannot {operation} outside your department. " +
                        $"Target folder belongs to '{targetDepartmentName}' but you belong to '{userDepartmentName}'. " +
                        $"Managers can only operate within their own department folders.");
                }

                _logger.LogInformation("Department boundary validation passed for user {UserId} to {Operation} in folder {FolderId}",
                    userId, operation, targetFolderId);
            }
            catch (Exception ex) when (!(ex is UnauthorizedAccessException || ex is KeyNotFoundException))
            {
                _logger.LogError(ex, "Error validating department boundary for user {UserId}", userId);
                throw new InvalidOperationException("Error validating department access", ex);
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

        private async Task<int> CountFoldersInTreeAsync(string rootFolderId, bool includeSystemFolders)
        {
            var count = await _unitOfWork.GetRepository<Folder>()
                .CountAsync(predicate: f => f.FullPath.StartsWith(rootFolderId) &&
                                           !f.IsDeleted &&
                                           (includeSystemFolders || !f.IsSystemFolder));
            return count;
        }

        private async Task<int> GetMaxDepthAsync(string rootFolderId)
        {
            var folders = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(predicate: f => f.FullPath.StartsWith(rootFolderId) && !f.IsDeleted);

            return folders.Any() ? folders.Max(f => f.Level) : 0;
        }

        private async Task UpdateFolderPathsAsync(Folder folder)
        {
            // Update this folder's path
            var parentPath = "";
            if (folder.ParentFolderId != null)
            {
                var parent = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(predicate: f => f.Id == folder.ParentFolderId);
                parentPath = parent?.FullPath ?? "";
            }

            var oldPath = folder.FullPath;
            folder.FullPath = FolderConstant.Helpers.BuildFullPath(parentPath, folder.Name);

            // Update all descendant paths
            var descendants = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(predicate: f => f.FullPath.StartsWith(oldPath + "/") && !f.IsDeleted);

            foreach (var descendant in descendants)
            {
                descendant.FullPath = descendant.FullPath.Replace(oldPath, folder.FullPath);
                await _unitOfWork.GetRepository<Folder>().UpdateAsync(descendant);
            }
        }

        private async Task<bool> IsDescendantFolderAsync(string potentialDescendantId, string ancestorId)
        {
            var descendant = await _unitOfWork.GetRepository<Folder>()
                .SingleOrDefaultAsync(predicate: f => f.Id == potentialDescendantId);

            if (descendant == null) return false;

            var ancestor = await _unitOfWork.GetRepository<Folder>()
                .SingleOrDefaultAsync(predicate: f => f.Id == ancestorId);

            if (ancestor == null) return false;

            return descendant.FullPath.StartsWith(ancestor.FullPath + "/");
        }

        private async Task InheritPermissionsFromParentAsync(string folderId, string parentFolderId)
        {
            var parentPermissions = await _unitOfWork.GetRepository<FolderPermission>()
                .GetListAsync(predicate: fp => fp.FolderId == parentFolderId && fp.IsActive);

            foreach (var parentPermission in parentPermissions)
            {
                var inheritedPermission = new FolderPermission
                {
                    FolderId = folderId,
                    UserId = parentPermission.UserId,
                    DepartmentId = parentPermission.DepartmentId,
                    PermissionType = parentPermission.PermissionType,
                    IsInherited = true,
                    ParentPermissionId = parentPermission.Id,
                    IsDenied = parentPermission.IsDenied,
                    ExpiresAt = parentPermission.ExpiresAt,
                    IsActive = true,
                    CreatedBy = "System",
                    CreatedTime = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<FolderPermission>().InsertAsync(inheritedPermission);
            }
        }

        private async Task ApplyPermissionToSubfoldersAsync(string folderId, SetFolderPermissionRequest request, string userId)
        {
            var subfolders = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(predicate: f => f.ParentFolderId == folderId && !f.IsDeleted);

            foreach (var subfolder in subfolders)
            {
                // Check if permission already exists
                var existingPermission = await _unitOfWork.GetRepository<FolderPermission>()
                    .SingleOrDefaultAsync(
                        predicate: fp => fp.FolderId == subfolder.Id && fp.IsActive &&
                                        fp.UserId == request.UserId && fp.DepartmentId == request.DepartmentId
                    );

                if (existingPermission != null)
                {
                    existingPermission.PermissionType = request.PermissionType;
                    existingPermission.IsDenied = request.IsDenied;
                    existingPermission.ExpiresAt = request.ExpiresAt;
                    existingPermission.LastUpdatedBy = userId;
                    existingPermission.LastUpdatedTime = DateTime.UtcNow;

                    await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(existingPermission);
                }
                else
                {
                    var newPermission = new FolderPermission
                    {
                        FolderId = subfolder.Id,
                        UserId = request.UserId,
                        DepartmentId = request.DepartmentId,
                        PermissionType = request.PermissionType,
                        IsDenied = request.IsDenied,
                        ExpiresAt = request.ExpiresAt,
                        IsActive = true,
                        CreatedBy = userId,
                        CreatedTime = DateTime.UtcNow
                    };

                    await _unitOfWork.GetRepository<FolderPermission>().InsertAsync(newPermission);
                }

                // Recursively apply to subfolders
                await ApplyPermissionToSubfoldersAsync(subfolder.Id, request, userId);
            }
        }

        #endregion

        #region Sync Verification

        public async Task<FolderSyncVerificationResult> VerifyFolderSyncAsync(string? departmentId = null)
        {
            try
            {
                _logger.LogInformation("Starting folder sync verification for department: {DepartmentId}", departmentId ?? "All");

                var result = new FolderSyncVerificationResult
                {
                    DepartmentId = departmentId
                };

                // Get folders from database
                var dbFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(predicate: f => !f.IsDeleted &&
                                                 (departmentId == null || f.DepartmentId == departmentId ||
                                                  (string.IsNullOrEmpty(departmentId) && f.IsPublic)));

                result.TotalFoldersChecked = dbFolders.Count;

                foreach (var folder in dbFolders)
                {
                    try
                    {
                        // Check if Google Drive folder exists
                        if (string.IsNullOrEmpty(folder.GoogleDriveFolderId))
                        {
                            result.Issues.Add(new FolderSyncIssue
                            {
                                FolderId = folder.Id,
                                FolderName = folder.Name,
                                IssueType = FolderSyncIssueType.InvalidGoogleDriveId,
                                Description = "Folder has no Google Drive ID",
                                CanAutoRepair = false,
                                DepartmentId = folder.DepartmentId,
                                FullPath = folder.FullPath
                            });
                            continue;
                        }

                        var googleDriveExists = await _googleDriveService.FolderExistsAsync(folder.GoogleDriveFolderId);
                        if (!googleDriveExists)
                        {
                            result.Issues.Add(new FolderSyncIssue
                            {
                                FolderId = folder.Id,
                                FolderName = folder.Name,
                                GoogleDriveFolderId = folder.GoogleDriveFolderId,
                                IssueType = FolderSyncIssueType.DatabaseOrphan,
                                Description = "Folder exists in database but not in Google Drive",
                                CanAutoRepair = true, // Can recreate in Google Drive
                                DepartmentId = folder.DepartmentId,
                                FullPath = folder.FullPath
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error checking sync for folder {FolderId} ({FolderName})", folder.Id, folder.Name);
                        result.Issues.Add(new FolderSyncIssue
                        {
                            FolderId = folder.Id,
                            FolderName = folder.Name,
                            GoogleDriveFolderId = folder.GoogleDriveFolderId,
                            IssueType = FolderSyncIssueType.MetadataMismatch,
                            Description = $"Error checking folder: {ex.Message}",
                            CanAutoRepair = false,
                            DepartmentId = folder.DepartmentId,
                            FullPath = folder.FullPath
                        });
                    }
                }

                result.SyncIssuesFound = result.Issues.Count;
                result.IsInSync = result.SyncIssuesFound == 0;

                _logger.LogInformation("Folder sync verification completed: {Summary}", result.Summary);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during folder sync verification");
                throw;
            }
        }

        /// <summary>
        /// Update documents in moved folder and all subfolders
        /// </summary>
        private async Task UpdateDocumentsInMovedFolderAsync(string movedFolderId, string oldFolderPath, string newFolderPath)
        {
            try
            {
                // Get all subfolders of the moved folder
                var allAffectedFolders = await GetAllSubfolderIdsAsync(movedFolderId);
                allAffectedFolders.Add(movedFolderId);

                _logger.LogInformation("Updating documents in {FolderCount} affected folders after move", allAffectedFolders.Count);

                // Get all documents in these folders (DocumentVersion doesn't have IsDeleted, filter by FolderId only)
                var documentsToUpdate = await _unitOfWork.GetRepository<DocumentVersion>()
                    .GetListAsync(predicate: dv => dv.FolderId != null && allAffectedFolders.Contains(dv.FolderId));

                foreach (var doc in documentsToUpdate)
                {
                    // Update timestamp to trigger any necessary updates
                    doc.LastUpdatedTime = DateTime.UtcNow;
                    await _unitOfWork.GetRepository<DocumentVersion>().UpdateAsync(doc);

                    // ✅ Update RAG index if approved document (using StatusEnum.Approved)
                    if (doc.Status == StatusEnum.Approved)
                    {
                        try
                        {
                            // Note: RAG service integration would be added here when available
                            // For now, just log the need for RAG update
                            _logger.LogDebug("Document {DocumentId} in moved folder needs RAG index update", doc.Id);

                            // TODO: Add RAG service integration
                            // await _documentRAGService.RemoveDocumentFromIndexAsync(doc.Id);
                            // await _documentRAGService.IndexDocumentWithFolderContextAsync(doc);
                        }
                        catch (Exception ragEx)
                        {
                            _logger.LogWarning(ragEx, "Failed to update RAG index for document {DocumentId} after folder move", doc.Id);
                            // Don't fail the entire operation for RAG issues
                        }
                    }
                }

                _logger.LogInformation("Successfully updated {DocumentCount} documents after folder move", documentsToUpdate.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating documents in moved folder {FolderId}", movedFolderId);
                throw;
            }
        }

        /// <summary>
        /// Get all subfolder IDs recursively
        /// </summary>
        private async Task<List<string>> GetAllSubfolderIdsAsync(string parentFolderId)
        {
            var result = new List<string>();
            var directSubfolders = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(predicate: f => f.ParentFolderId == parentFolderId && !f.IsDeleted);

            foreach (var subfolder in directSubfolders)
            {
                result.Add(subfolder.Id);
                var nestedSubfolders = await GetAllSubfolderIdsAsync(subfolder.Id);
                result.AddRange(nestedSubfolders);
            }

            return result;
        }

        #endregion
    }
}
