using Document.API.Constants;
using Document.API.Payload.Request.Admin;
using Document.API.Payload.Response.Admin;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Models;
using Document.Domain.Enums;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;
using MassTransit;
using Shared.Command;
using Shared.DTOs;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Service implementation for admin management of manager permissions to public folders
    /// </summary>
    public class AdminPublicFolderService : IAdminPublicFolderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AdminPublicFolderService> _logger;
        private readonly IRequestClient<GetUserByIdCommand> _getUserClient;
        private readonly IFolderPermissionEnrichmentService _folderPermissionEnrichmentService;

        public AdminPublicFolderService(
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AdminPublicFolderService> logger,
            IRequestClient<GetUserByIdCommand> getUserClient,
            IFolderPermissionEnrichmentService folderPermissionEnrichmentService)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _getUserClient = getUserClient;
            _folderPermissionEnrichmentService = folderPermissionEnrichmentService;
        }

        public async Task<ManagerPublicFolderPermissionResponse> GrantManagerPermissionAsync(GrantManagerPublicFolderPermissionRequest request)
        {
            try
            {
                var adminUserId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                _logger.LogInformation("Admin {AdminUserId} granting {PermissionType} permission to manager {ManagerUserId} for public folder {PublicFolderId}",
                    adminUserId, request.PermissionType, request.ManagerUserId, request.PublicFolderId ?? "ALL");

                // Validate that the target user is actually a manager
                if (!await ValidateUserIsManagerAsync(request.ManagerUserId))
                {
                    throw new ArgumentException($"User {request.ManagerUserId} is not a manager");
                }

                // If specific folder ID provided, validate it's a public folder
                if (!string.IsNullOrEmpty(request.PublicFolderId))
                {
                    var targetFolder = await _unitOfWork.GetRepository<Folder>()
                        .SingleOrDefaultAsync(predicate: f => f.Id == request.PublicFolderId && !f.IsDeleted);

                    if (targetFolder == null)
                    {
                        throw new KeyNotFoundException($"Folder {request.PublicFolderId} not found");
                    }

                    if (!targetFolder.IsPublic)
                    {
                        throw new ArgumentException($"Folder {request.PublicFolderId} is not a public folder");
                    }

                    // Grant permission to specific folder
                    var permission = await CreateFolderPermissionAsync(request.ManagerUserId, request.PublicFolderId, request.PermissionType, request.ExpiresAt, adminUserId, request.Reason);

                    // Apply to subfolders if requested
                    if (request.ApplyToSubfolders)
                    {
                        await ApplyPermissionToSubfoldersAsync(request.PublicFolderId, request.ManagerUserId, request.PermissionType, request.ExpiresAt, adminUserId);
                    }

                    await _unitOfWork.CommitAsync();

                    var response = await MapToManagerPublicFolderPermissionResponseAsync(permission);
                    _logger.LogInformation("Successfully granted {PermissionType} permission to manager {ManagerUserId} for public folder {PublicFolderId}",
                        request.PermissionType, request.ManagerUserId, request.PublicFolderId);

                    return response;
                }
                else
                {
                    // Grant permission to all public folders
                    var publicFolders = await _unitOfWork.GetRepository<Folder>()
                        .GetListAsync(predicate: f => f.IsPublic && !f.IsDeleted);

                    if (!publicFolders.Any())
                    {
                        throw new InvalidOperationException("No public folders found in the system");
                    }

                    FolderPermission? lastCreatedPermission = null;
                    foreach (var folder in publicFolders)
                    {
                        lastCreatedPermission = await CreateFolderPermissionAsync(request.ManagerUserId, folder.Id, request.PermissionType, request.ExpiresAt, adminUserId, request.Reason);
                    }

                    await _unitOfWork.CommitAsync();

                    var response = await MapToManagerPublicFolderPermissionResponseAsync(lastCreatedPermission!);
                    response.PublicFolderName = "All Public Folders";
                    response.PublicFolderPath = "All Public Folders";

                    _logger.LogInformation("Successfully granted {PermissionType} permission to manager {ManagerUserId} for all {Count} public folders",
                        request.PermissionType, request.ManagerUserId, publicFolders.Count);

                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting permission to manager {ManagerUserId} for public folder {PublicFolderId}",
                    request.ManagerUserId, request.PublicFolderId);
                throw;
            }
        }

        public async Task<bool> RevokeManagerPermissionAsync(RevokeManagerPublicFolderPermissionRequest request)
        {
            try
            {
                var adminUserId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                _logger.LogInformation("Admin {AdminUserId} revoking permissions from manager {ManagerUserId} for public folder {PublicFolderId}",
                    adminUserId, request.ManagerUserId, request.PublicFolderId ?? "ALL");

                if (!string.IsNullOrEmpty(request.PublicFolderId))
                {
                    // Revoke permission from specific folder
                    var revokedCount = await RevokePermissionsFromFolderAsync(request.ManagerUserId, request.PublicFolderId, adminUserId);

                    // Revoke from subfolders if requested
                    if (request.RevokeFromSubfolders)
                    {
                        revokedCount += await RevokePermissionsFromSubfoldersAsync(request.PublicFolderId, request.ManagerUserId, adminUserId);
                    }

                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Successfully revoked {Count} permissions from manager {ManagerUserId} for public folder {PublicFolderId}",
                        revokedCount, request.ManagerUserId, request.PublicFolderId);

                    return revokedCount > 0;
                }
                else
                {
                    // Revoke permissions from all public folders
                    var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                        .GetListAsync(predicate: fp => fp.UserId == request.ManagerUserId && fp.IsActive &&
                                                      fp.Folder.IsPublic && !fp.Folder.IsDeleted,
                                     include: i => i.Include(fp => fp.Folder));

                    foreach (var permission in permissions)
                    {
                        permission.IsActive = false;
                        permission.LastUpdatedBy = adminUserId;
                        permission.LastUpdatedTime = DateTime.UtcNow;
                        await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(permission);
                    }

                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Successfully revoked {Count} permissions from manager {ManagerUserId} for all public folders",
                        permissions.Count, request.ManagerUserId);

                    return permissions.Any();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking permissions from manager {ManagerUserId} for public folder {PublicFolderId}",
                    request.ManagerUserId, request.PublicFolderId);
                throw;
            }
        }

        public async Task<BulkManagerPermissionOperationResponse> BulkGrantManagerPermissionsAsync(BulkGrantManagerPermissionsRequest request)
        {
            var response = new BulkManagerPermissionOperationResponse
            {
                TotalManagers = request.ManagerUserIds.Count,
                Message = "Bulk permission grant operation completed"
            };

            try
            {
                var adminUserId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                _logger.LogInformation("Admin {AdminUserId} performing bulk grant of {PermissionType} permissions to {Count} managers for public folder {PublicFolderId}",
                    adminUserId, request.PermissionType, request.ManagerUserIds.Count, request.PublicFolderId ?? "ALL");

                foreach (var managerUserId in request.ManagerUserIds)
                {
                    try
                    {
                        // Validate that the user is a manager
                        if (!await ValidateUserIsManagerAsync(managerUserId))
                        {
                            response.Errors.Add($"User {managerUserId} is not a manager");
                            response.FailedOperations++;
                            continue;
                        }

                        var grantRequest = new GrantManagerPublicFolderPermissionRequest
                        {
                            ManagerUserId = managerUserId,
                            PublicFolderId = request.PublicFolderId,
                            PermissionType = request.PermissionType,
                            ExpiresAt = request.ExpiresAt,
                            ApplyToSubfolders = request.ApplyToSubfolders,
                            Reason = request.Reason
                        };

                        var permissionResponse = await GrantManagerPermissionAsync(grantRequest);
                        response.ProcessedPermissions.Add(permissionResponse);
                        response.SuccessfulOperations++;
                    }
                    catch (Exception ex)
                    {
                        response.Errors.Add($"Failed to grant permission to manager {managerUserId}: {ex.Message}");
                        response.FailedOperations++;
                        _logger.LogWarning(ex, "Failed to grant permission to manager {ManagerUserId} in bulk operation", managerUserId);
                    }
                }

                response.Success = response.FailedOperations == 0;
                response.OperationDetails["PublicFolderId"] = request.PublicFolderId ?? "ALL";
                response.OperationDetails["PermissionType"] = request.PermissionType.ToString();
                response.OperationDetails["ApplyToSubfolders"] = request.ApplyToSubfolders;

                _logger.LogInformation("Bulk permission grant completed: {SuccessfulOperations}/{TotalManagers} successful",
                    response.SuccessfulOperations, response.TotalManagers);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk grant permissions operation");
                response.Success = false;
                response.Errors.Add($"Bulk operation failed: {ex.Message}");
                return response;
            }
        }

        public async Task<List<ManagerPublicFolderPermissionResponse>> GetManagerPublicFolderPermissionsAsync(GetManagerPublicFolderPermissionsRequest request)
        {
            try
            {
                _logger.LogInformation("Getting manager public folder permissions - Manager: {ManagerUserId}, Folder: {PublicFolderId}",
                    request.ManagerUserId ?? "ALL", request.PublicFolderId ?? "ALL");

                var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(
                        predicate: fp => fp.Folder.IsPublic && !fp.Folder.IsDeleted &&
                                        (request.ManagerUserId == null || fp.UserId == request.ManagerUserId) &&
                                        (request.PublicFolderId == null || fp.FolderId == request.PublicFolderId) &&
                                        (request.IncludeExpired || fp.IsValid) &&
                                        (fp.IsActive || request.IncludeExpired),
                        include: i => i.Include(fp => fp.Folder)
                    );

                if (!request.IncludeInherited)
                {
                    permissions = permissions.Where(fp => !fp.IsInherited).ToList();
                }

                var responses = new List<ManagerPublicFolderPermissionResponse>();
                foreach (var permission in permissions)
                {
                    var response = await MapToManagerPublicFolderPermissionResponseAsync(permission);
                    responses.Add(response);
                }

                _logger.LogInformation("Retrieved {Count} manager public folder permissions", responses.Count);
                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting manager public folder permissions");
                throw;
            }
        }

        public async Task<ManagerPublicFolderAccessSummary> GetManagerAccessSummaryAsync(string managerUserId)
        {
            try
            {
                _logger.LogInformation("Getting access summary for manager {ManagerUserId}", managerUserId);

                // Validate that the user is a manager
                if (!await ValidateUserIsManagerAsync(managerUserId))
                {
                    throw new ArgumentException($"User {managerUserId} is not a manager");
                }

                var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(
                        predicate: fp => fp.UserId == managerUserId && fp.Folder.IsPublic && 
                                        !fp.Folder.IsDeleted && fp.IsValid && fp.IsActive,
                        include: i => i.Include(fp => fp.Folder)
                    );

                var summary = new ManagerPublicFolderAccessSummary
                {
                    ManagerUserId = managerUserId,
                    TotalAccessibleFolders = permissions.Count,
                    EditPermissionFolders = permissions.Count(p => p.PermissionType.Includes(PermissionType.Edit)),
                    DeletePermissionFolders = permissions.Count(p => p.PermissionType.Includes(PermissionType.Delete)),
                    ManagePermissionFolders = permissions.Count(p => p.PermissionType.Includes(PermissionType.Manage)),
                    HighestPermission = permissions.Any() ? permissions.Max(p => p.PermissionType) : null,
                    LastUpdated = DateTime.UtcNow
                };

                // Get user details
                try
                {
                    var userResponse = await _getUserClient.GetResponse<GetUserByIdResponse>(new GetUserByIdCommand { UserId = Guid.Parse(managerUserId) });
                    if (userResponse.Message.Success && userResponse.Message.User != null)
                    {
                        var user = userResponse.Message.User;
                        summary.ManagerUserName = user.Name;
                        summary.ManagerUserEmail = user.Email;
                        summary.DepartmentName = user.DepartmentName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve user details for manager {ManagerUserId}", managerUserId);
                }

                // Map permissions
                foreach (var permission in permissions)
                {
                    var permissionResponse = await MapToManagerPublicFolderPermissionResponseAsync(permission);
                    summary.FolderPermissions.Add(permissionResponse);
                }

                _logger.LogInformation("Generated access summary for manager {ManagerUserId}: {TotalFolders} accessible folders",
                    managerUserId, summary.TotalAccessibleFolders);

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access summary for manager {ManagerUserId}", managerUserId);
                throw;
            }
        }

        public async Task<PublicFolderManagerAccessOverview> GetPublicFolderManagerAccessOverviewAsync(string publicFolderId)
        {
            try
            {
                _logger.LogInformation("Getting manager access overview for public folder {PublicFolderId}", publicFolderId);

                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(predicate: f => f.Id == publicFolderId && !f.IsDeleted);

                if (folder == null)
                {
                    throw new KeyNotFoundException($"Folder {publicFolderId} not found");
                }

                if (!folder.IsPublic)
                {
                    throw new ArgumentException($"Folder {publicFolderId} is not a public folder");
                }

                var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(
                        predicate: fp => fp.FolderId == publicFolderId && fp.IsValid && fp.IsActive,
                        include: i => i.Include(fp => fp.Folder)
                    );

                // Filter to only manager permissions
                var managerPermissions = new List<FolderPermission>();
                foreach (var permission in permissions)
                {
                    if (!string.IsNullOrEmpty(permission.UserId) && await ValidateUserIsManagerAsync(permission.UserId))
                    {
                        managerPermissions.Add(permission);
                    }
                }

                var overview = new PublicFolderManagerAccessOverview
                {
                    PublicFolderId = publicFolderId,
                    PublicFolderName = folder.Name,
                    PublicFolderPath = folder.FullPath,
                    TotalManagersWithAccess = managerPermissions.GroupBy(p => p.UserId).Count(),
                    ManagersWithEdit = managerPermissions.Count(p => p.PermissionType.Includes(PermissionType.Edit)),
                    ManagersWithDelete = managerPermissions.Count(p => p.PermissionType.Includes(PermissionType.Delete)),
                    ManagersWithManage = managerPermissions.Count(p => p.PermissionType.Includes(PermissionType.Manage)),
                    HasInheritedPermissions = managerPermissions.Any(p => p.IsInherited),
                    SubfolderCount = folder.SubFolderCount
                };

                // Group permissions by manager and create summaries
                var managerGroups = managerPermissions.GroupBy(p => p.UserId);
                foreach (var managerGroup in managerGroups)
                {
                    var managerId = managerGroup.Key;
                    if (!string.IsNullOrEmpty(managerId))
                    {
                        var managerSummary = await GetManagerAccessSummaryAsync(managerId);
                        overview.ManagerAccess.Add(managerSummary);
                    }
                }

                _logger.LogInformation("Generated overview for public folder {PublicFolderId}: {ManagerCount} managers have access",
                    publicFolderId, overview.TotalManagersWithAccess);

                return overview;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting manager access overview for public folder {PublicFolderId}", publicFolderId);
                throw;
            }
        }

        public async Task<List<ManagerPublicFolderAccessSummary>> GetAllManagersWithPublicFolderAccessAsync()
        {
            try
            {
                _logger.LogInformation("Getting all managers with public folder access");

                // Get all permissions to public folders
                var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(
                        predicate: fp => fp.Folder.IsPublic && !fp.Folder.IsDeleted && 
                                        fp.IsValid && fp.IsActive && !string.IsNullOrEmpty(fp.UserId),
                        include: i => i.Include(fp => fp.Folder)
                    );

                // Group by user and filter to only managers
                var managerIds = permissions.GroupBy(p => p.UserId).Select(g => g.Key).ToList();
                var summaries = new List<ManagerPublicFolderAccessSummary>();

                foreach (var managerId in managerIds)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(managerId) && await ValidateUserIsManagerAsync(managerId))
                        {
                            var summary = await GetManagerAccessSummaryAsync(managerId);
                            summaries.Add(summary);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not generate summary for user {UserId}", managerId);
                    }
                }

                _logger.LogInformation("Retrieved {Count} managers with public folder access", summaries.Count);
                return summaries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all managers with public folder access");
                throw;
            }
        }

        public async Task<bool> CheckManagerPermissionAsync(string managerUserId, string publicFolderId, PermissionType requiredPermission)
        {
            try
            {
                // Validate that the user is a manager
                if (!await ValidateUserIsManagerAsync(managerUserId))
                {
                    return false;
                }

                // Check explicit permission
                var permission = await _unitOfWork.GetRepository<FolderPermission>()
                    .SingleOrDefaultAsync(
                        predicate: fp => fp.UserId == managerUserId && fp.FolderId == publicFolderId &&
                                        fp.IsValid && fp.IsActive && !fp.IsDenied,
                        include: i => i.Include(fp => fp.Folder)
                    );

                if (permission?.Folder?.IsPublic == true)
                {
                    return permission.PermissionType.Includes(requiredPermission);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking manager {ManagerUserId} permission for folder {PublicFolderId}",
                    managerUserId, publicFolderId);
                return false;
            }
        }

        public async Task<List<ManagerPublicFolderPermissionResponse>> GetManagerAccessiblePublicFoldersAsync(string managerUserId, PermissionType minimumPermission = PermissionType.View)
        {
            try
            {
                _logger.LogInformation("Getting accessible public folders for manager {ManagerUserId} with minimum permission {MinimumPermission}",
                    managerUserId, minimumPermission);

                if (!await ValidateUserIsManagerAsync(managerUserId))
                {
                    throw new ArgumentException($"User {managerUserId} is not a manager");
                }

                var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(
                        predicate: fp => fp.UserId == managerUserId && fp.Folder.IsPublic && 
                                        !fp.Folder.IsDeleted && fp.IsValid && fp.IsActive &&
                                        fp.PermissionType >= minimumPermission,
                        include: i => i.Include(fp => fp.Folder)
                    );

                var responses = new List<ManagerPublicFolderPermissionResponse>();
                foreach (var permission in permissions)
                {
                    var response = await MapToManagerPublicFolderPermissionResponseAsync(permission);
                    responses.Add(response);
                }

                _logger.LogInformation("Found {Count} accessible public folders for manager {ManagerUserId}",
                    responses.Count, managerUserId);

                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accessible public folders for manager {ManagerUserId}", managerUserId);
                throw;
            }
        }

        public async Task<List<ManagerPublicFolderPermissionResponse>> GetPermissionAuditTrailAsync(string? managerUserId = null, string? publicFolderId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                _logger.LogInformation("Getting permission audit trail - Manager: {ManagerUserId}, Folder: {PublicFolderId}, From: {FromDate}, To: {ToDate}",
                    managerUserId ?? "ALL", publicFolderId ?? "ALL", fromDate, toDate);

                var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(
                        predicate: fp => fp.Folder.IsPublic && !fp.Folder.IsDeleted &&
                                        (managerUserId == null || fp.UserId == managerUserId) &&
                                        (publicFolderId == null || fp.FolderId == publicFolderId) &&
                                        (fromDate == null || fp.CreatedTime >= fromDate) &&
                                        (toDate == null || fp.CreatedTime <= toDate),
                        include: i => i.Include(fp => fp.Folder),
                        orderBy: o => o.OrderByDescending(fp => fp.CreatedTime)
                    );

                var responses = new List<ManagerPublicFolderPermissionResponse>();
                foreach (var permission in permissions)
                {
                    // Only include if user is/was a manager
                    if (!string.IsNullOrEmpty(permission.UserId) && await ValidateUserIsManagerAsync(permission.UserId))
                    {
                        var response = await MapToManagerPublicFolderPermissionResponseAsync(permission);
                        responses.Add(response);
                    }
                }

                _logger.LogInformation("Retrieved {Count} audit trail entries", responses.Count);
                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permission audit trail");
                throw;
            }
        }

        public async Task<bool> ValidateUserIsManagerAsync(string userId)
        {
            try
            {
                var userResponse = await _getUserClient.GetResponse<GetUserByIdResponse>(new GetUserByIdCommand { UserId = Guid.Parse(userId) });
                
                if (userResponse.Message.Success && userResponse.Message.User != null)
                {
                    var user = userResponse.Message.User;
                    return user.Role == "Manager" || user.Role == "Admin";
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not validate user {UserId} role", userId);
                return false;
            }
        }

        public async Task<List<ManagerPublicFolderPermissionResponse>> GetAllPublicFoldersAsync()
        {
            try
            {
                _logger.LogInformation("Getting all public folders");

                var publicFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(predicate: f => f.IsPublic && !f.IsDeleted);

                var responses = new List<ManagerPublicFolderPermissionResponse>();
                foreach (var folder in publicFolders)
                {
                    var response = new ManagerPublicFolderPermissionResponse
                    {
                        PublicFolderId = folder.Id,
                        PublicFolderName = folder.Name,
                        PublicFolderPath = folder.FullPath,
                        PermissionType = PermissionType.View, // Default for listing
                        PermissionDescription = "Public folder available for permission assignment",
                        IsActive = true,
                        IsValid = true,
                        GrantedTime = folder.CreatedTime,
                        ManagerUserId = string.Empty // Will be filled when assigned to a manager
                    };
                    responses.Add(response);
                }

                _logger.LogInformation("Retrieved {Count} public folders", responses.Count);
                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all public folders");
                throw;
            }
        }

        public async Task<int> CleanupExpiredPermissionsAsync()
        {
            try
            {
                _logger.LogInformation("Cleaning up expired permissions");

                var expiredPermissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(
                        predicate: fp => fp.IsActive && fp.ExpiresAt != null && fp.ExpiresAt <= DateTime.UtcNow &&
                                        fp.Folder.IsPublic && !fp.Folder.IsDeleted,
                        include: i => i.Include(fp => fp.Folder)
                    );

                var adminUserId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                foreach (var permission in expiredPermissions)
                {
                    permission.IsActive = false;
                    permission.LastUpdatedBy = adminUserId;
                    permission.LastUpdatedTime = DateTime.UtcNow;
                    await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(permission);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Cleaned up {Count} expired permissions", expiredPermissions.Count);
                return expiredPermissions.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired permissions");
                throw;
            }
        }

        #region Private Helper Methods

        private async Task<FolderPermission> CreateFolderPermissionAsync(string managerUserId, string publicFolderId, PermissionType permissionType, DateTime? expiresAt, string adminUserId, string? reason)
        {
            // Check if permission already exists
            var existingPermission = await _unitOfWork.GetRepository<FolderPermission>()
                .SingleOrDefaultAsync(predicate: fp => fp.UserId == managerUserId && fp.FolderId == publicFolderId && fp.IsActive);

            if (existingPermission != null)
            {
                // Update existing permission
                existingPermission.PermissionType = permissionType;
                existingPermission.ExpiresAt = expiresAt;
                existingPermission.LastUpdatedBy = adminUserId;
                existingPermission.LastUpdatedTime = DateTime.UtcNow;
                await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(existingPermission);
                return existingPermission;
            }
            else
            {
                // Create new permission
                var newPermission = new FolderPermission
                {
                    FolderId = publicFolderId,
                    UserId = managerUserId,
                    PermissionType = permissionType,
                    ExpiresAt = expiresAt,
                    IsActive = true,
                    IsInherited = false,
                    IsDenied = false,
                    CreatedBy = adminUserId,
                    CreatedTime = DateTime.UtcNow
                };

                await _unitOfWork.GetRepository<FolderPermission>().InsertAsync(newPermission);
                return newPermission;
            }
        }

        private async Task ApplyPermissionToSubfoldersAsync(string parentFolderId, string managerUserId, PermissionType permissionType, DateTime? expiresAt, string adminUserId)
        {
            var subfolders = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(predicate: f => f.ParentFolderId == parentFolderId && f.IsPublic && !f.IsDeleted);

            foreach (var subfolder in subfolders)
            {
                await CreateFolderPermissionAsync(managerUserId, subfolder.Id, permissionType, expiresAt, adminUserId, "Applied from parent folder");
                
                // Recursively apply to subfolders
                await ApplyPermissionToSubfoldersAsync(subfolder.Id, managerUserId, permissionType, expiresAt, adminUserId);
            }
        }

        private async Task<int> RevokePermissionsFromFolderAsync(string managerUserId, string publicFolderId, string adminUserId)
        {
            var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                .GetListAsync(predicate: fp => fp.UserId == managerUserId && fp.FolderId == publicFolderId && fp.IsActive);

            foreach (var permission in permissions)
            {
                permission.IsActive = false;
                permission.LastUpdatedBy = adminUserId;
                permission.LastUpdatedTime = DateTime.UtcNow;
                await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(permission);
            }

            return permissions.Count;
        }

        private async Task<int> RevokePermissionsFromSubfoldersAsync(string parentFolderId, string managerUserId, string adminUserId)
        {
            var subfolders = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(predicate: f => f.ParentFolderId == parentFolderId && f.IsPublic && !f.IsDeleted);

            int revokedCount = 0;
            foreach (var subfolder in subfolders)
            {
                revokedCount += await RevokePermissionsFromFolderAsync(managerUserId, subfolder.Id, adminUserId);
                
                // Recursively revoke from subfolders
                revokedCount += await RevokePermissionsFromSubfoldersAsync(subfolder.Id, managerUserId, adminUserId);
            }

            return revokedCount;
        }

        private async Task<ManagerPublicFolderPermissionResponse> MapToManagerPublicFolderPermissionResponseAsync(FolderPermission permission)
        {
            var response = new ManagerPublicFolderPermissionResponse
            {
                Id = permission.Id,
                ManagerUserId = permission.UserId ?? string.Empty,
                PublicFolderId = permission.FolderId,
                PublicFolderName = permission.Folder?.Name,
                PublicFolderPath = permission.Folder?.FullPath,
                PermissionType = permission.PermissionType,
                PermissionDescription = permission.PermissionType.GetDescription(),
                IsInherited = permission.IsInherited,
                ExpiresAt = permission.ExpiresAt,
                IsActive = permission.IsActive,
                IsValid = permission.IsValid,
                GrantedBy = permission.CreatedBy,
                GrantedTime = permission.CreatedTime,
                LastUpdatedTime = permission.LastUpdatedTime
            };

            // Enrich with user details if needed
            if (!string.IsNullOrEmpty(permission.UserId))
            {
                try
                {
                    var userResponse = await _getUserClient.GetResponse<GetUserByIdResponse>(new GetUserByIdCommand { UserId = Guid.Parse(permission.UserId) });
                    if (userResponse.Message.Success && userResponse.Message.User != null)
                    {
                        var user = userResponse.Message.User;
                        response.ManagerUserName = user.Name;
                        response.ManagerUserEmail = user.Email;
                        response.ManagerDepartmentId = user.DepartmentId?.ToString();
                        response.ManagerDepartmentName = user.DepartmentName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve user details for {UserId}", permission.UserId);
                }
            }

            return response;
        }

        #endregion
    }
}
