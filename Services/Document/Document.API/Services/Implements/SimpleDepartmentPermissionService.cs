using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Enums;
using Document.Domain.Models;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Document.API.Services.Implements
{
    /// <summary>
    /// ✅ SIMPLE DEPARTMENT-BASED PERMISSION SERVICE
    /// Implements the simple permission model:
    /// - All department members can VIEW their department's files/folders
    /// - Only specific users get EDIT permissions (managed by managers)
    /// - Managers have full control over their department
    /// </summary>
    public class SimpleDepartmentPermissionService : ISimpleDepartmentPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SimpleDepartmentPermissionService> _logger;

        public SimpleDepartmentPermissionService(
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor,
            ILogger<SimpleDepartmentPermissionService> logger)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// Check if user can access folder/document based on simple department rules
        /// </summary>
        public async Task<bool> CanUserAccessAsync(string userId, string userDepartmentId, string resourceDepartmentId, PermissionType requiredPermission)
        {
            try
            {
                // ✅ 1. SAME DEPARTMENT: All members can view, only specific users can edit
                if (resourceDepartmentId == userDepartmentId)
                {
                    // Everyone in department can VIEW
                    if (requiredPermission == PermissionType.View)
                    {
                        return true;
                    }

                    // Managers can do everything
                    var userRole = JwtTokenHelper.GetUserRole(_httpContextAccessor);
                    if (userRole == "Manager")
                    {
                        return true;
                    }

                    // For EDIT/DELETE/MANAGE: Check if user has explicit permission
                    return await HasExplicitPermissionAsync(userId, requiredPermission);
                }

                // ✅ 2. OTHER DEPARTMENTS: No access unless explicitly granted
                return await HasExplicitPermissionAsync(userId, requiredPermission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking access for user {UserId} on resource in department {DepartmentId}", userId, resourceDepartmentId);
                return false;
            }
        }

        /// <summary>
        /// Grant EDIT permission to a specific user (only managers can do this)
        /// </summary>
        public async Task<bool> GrantEditPermissionAsync(string folderId, string targetUserId, string grantedByUserId)
        {
            try
            {
                // Check if granter is manager
                var granterRole = JwtTokenHelper.GetUserRole(_httpContextAccessor);
                if (granterRole != "Manager")
                {
                    throw new UnauthorizedAccessException("Only managers can grant edit permissions");
                }

                // Create or update permission
                var existingPermission = await _unitOfWork.GetRepository<FolderPermission>()
                    .SingleOrDefaultAsync(predicate: fp => fp.FolderId == folderId && fp.UserId == targetUserId && fp.IsActive);

                if (existingPermission != null)
                {
                    existingPermission.PermissionType = PermissionType.Edit;
                    existingPermission.LastUpdatedTime = DateTime.UtcNow;
                    existingPermission.LastUpdatedBy = grantedByUserId;
                    await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(existingPermission);
                }
                else
                {
                    var newPermission = new FolderPermission
                    {
                        Id = Guid.NewGuid().ToString(),
                        FolderId = folderId,
                        UserId = targetUserId,
                        PermissionType = PermissionType.Edit,
                        IsActive = true,
                        IsDenied = false,
                        IsInherited = false,
                        CreatedTime = DateTime.UtcNow,
                        CreatedBy = grantedByUserId
                    };
                    await _unitOfWork.GetRepository<FolderPermission>().InsertAsync(newPermission);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Granted EDIT permission to user {UserId} on folder {FolderId} by {GrantedBy}", 
                    targetUserId, folderId, grantedByUserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting edit permission to user {UserId} on folder {FolderId}", targetUserId, folderId);
                throw;
            }
        }

        /// <summary>
        /// Revoke EDIT permission from a specific user (only managers can do this)
        /// </summary>
        public async Task<bool> RevokeEditPermissionAsync(string folderId, string targetUserId, string revokedByUserId)
        {
            try
            {
                // Check if revoker is manager
                var revokerRole = JwtTokenHelper.GetUserRole(_httpContextAccessor);
                if (revokerRole != "Manager")
                {
                    throw new UnauthorizedAccessException("Only managers can revoke edit permissions");
                }

                var permission = await _unitOfWork.GetRepository<FolderPermission>()
                    .SingleOrDefaultAsync(predicate: fp => fp.FolderId == folderId && fp.UserId == targetUserId && fp.IsActive);

                if (permission != null)
                {
                    permission.IsActive = false;
                    permission.LastUpdatedTime = DateTime.UtcNow;
                    permission.LastUpdatedBy = revokedByUserId;
                    await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(permission);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Revoked EDIT permission from user {UserId} on folder {FolderId} by {RevokedBy}", 
                        targetUserId, folderId, revokedByUserId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking edit permission from user {UserId} on folder {FolderId}", targetUserId, folderId);
                throw;
            }
        }

        /// <summary>
        /// Get all users with EDIT permissions in a folder (for managers to see)
        /// </summary>
        public async Task<List<string>> GetUsersWithEditPermissionAsync(string folderId)
        {
            try
            {
                var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(predicate: fp => fp.FolderId == folderId && fp.IsActive && 
                                                  fp.PermissionType >= PermissionType.Edit && 
                                                  !fp.IsDenied);

                return permissions.Where(p => !string.IsNullOrEmpty(p.UserId))
                                .Select(p => p.UserId!)
                                .Distinct()
                                .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users with edit permission for folder {FolderId}", folderId);
                throw;
            }
        }

        #region Private Methods

        /// <summary>
        /// Check if user has explicit permission (not department default)
        /// </summary>
        private async Task<bool> HasExplicitPermissionAsync(string userId, PermissionType requiredPermission)
        {
            try
            {
                var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(predicate: fp => fp.UserId == userId && fp.IsActive && !fp.IsDenied);

                return permissions.Any(p => p.PermissionType.Includes(requiredPermission));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking explicit permission for user {UserId}", userId);
                return false;
            }
        }

        #endregion
    }
}
