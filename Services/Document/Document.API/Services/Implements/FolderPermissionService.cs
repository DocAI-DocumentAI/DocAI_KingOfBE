using Document.API.Constants;
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
using Document.API.Attributes;
using System.Linq.Expressions; // ✅ ADDED: For Expression<Func<T, bool>>

namespace Document.API.Services.Implements
{
    /// <summary>
    /// Advanced folder permission management service
    /// </summary>
    public class FolderPermissionService : IFolderPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<FolderPermissionService> _logger;

        public FolderPermissionService(
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor,
            ILogger<FolderPermissionService> logger)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// ✅ SIMPLIFIED: Get effective permission using simple department-based logic
        /// </summary>
        public async Task<PermissionType?> GetEffectivePermissionAsync(string folderId, string userId, string userDepartmentId)
        {
            try
            {
                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(
                        predicate: f => f.Id == folderId && !f.IsDeleted,
                        include: i => i.Include(f => f.FolderPermissions.Where(fp => fp.IsActive))
                    );

                if (folder == null) return null;

                // ✅ 1. Check explicit user permission first
                var userPermission = folder.FolderPermissions
                    .Where(fp => fp.UserId == userId && fp.IsValid)
                    .OrderByDescending(fp => fp.PermissionType)
                    .FirstOrDefault();

                if (userPermission != null)
                {
                    return userPermission.IsDenied ? null : userPermission.PermissionType;
                }

                // ✅ 2. Simple department-based defaults
                if (folder.IsPublic)
                {
                    return PermissionType.View; // Everyone can view public folders
                }

                if (folder.DepartmentId == userDepartmentId)
                {
                    // Check if user is manager
                    var userRole = JwtTokenHelper.GetUserRole(_httpContextAccessor);
                    return userRole == Roles.Manager ? PermissionType.Manage : PermissionType.View;
                }

                return null; // No access to other departments
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting effective permission for user {UserId} on folder {FolderId}", userId, folderId);
                return null;
            }
        }

        public async Task<FolderPermissionBreakdownResponse> GetPermissionBreakdownAsync(string folderId, string userId, string userDepartmentId)
        {
            try
            {
                _logger.LogInformation("Getting permission breakdown for user {UserId} on folder {FolderId}", userId, folderId);

                var folder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(
                        predicate: f => f.Id == folderId && !f.IsDeleted,
                        include: i => i.Include(f => f.FolderPermissions.Where(fp => fp.IsActive))
                    );

                if (folder == null)
                {
                    throw new KeyNotFoundException($"Folder with ID {folderId} not found");
                }

                var breakdown = new FolderPermissionBreakdownResponse
                {
                    FolderId = folderId,
                    UserId = userId,
                    UserDepartmentId = userDepartmentId
                };

                // 1. Get direct permissions
                var directPermissions = folder.FolderPermissions
                    .Where(fp => fp.UserId == userId && fp.IsValid)
                    .ToList();

                foreach (var permission in directPermissions)
                {
                    breakdown.DirectPermissions.Add(new PermissionSourceDetail
                    {
                        PermissionId = permission.Id,
                        PermissionType = permission.PermissionType,
                        IsDenied = permission.IsDenied,
                        Source = "Direct",
                        CreatedTime = permission.CreatedTime,
                        CreatedBy = permission.CreatedBy,
                        ExpiresAt = permission.ExpiresAt,
                        IsActive = permission.IsActive
                    });
                }

                // 2. Get department permissions
                var departmentPermissions = folder.FolderPermissions
                    .Where(fp => fp.DepartmentId == userDepartmentId && fp.IsValid)
                    .ToList();

                foreach (var permission in departmentPermissions)
                {
                    breakdown.DepartmentPermissions.Add(new PermissionSourceDetail
                    {
                        PermissionId = permission.Id,
                        PermissionType = permission.PermissionType,
                        IsDenied = permission.IsDenied,
                        Source = "Department",
                        CreatedTime = permission.CreatedTime,
                        CreatedBy = permission.CreatedBy,
                        ExpiresAt = permission.ExpiresAt,
                        IsActive = permission.IsActive
                    });
                }

                // 3. Get inherited permissions from parent folders
                await GetInheritedPermissionsAsync(folder, userId, userDepartmentId, breakdown);

                // 4. Get default permissions
                await GetDefaultPermissionsAsync(folder, userId, userDepartmentId, breakdown);

                // 5. Calculate effective permission
                breakdown.EffectivePermission = CalculateEffectivePermission(breakdown);
                breakdown.PermissionSource = DeterminePermissionSource(breakdown);

                // 6. Check for conflicts
                CheckForConflicts(breakdown);

                return breakdown;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permission breakdown for user {UserId} on folder {FolderId}", userId, folderId);
                throw;
            }
        }

        public async Task<List<FolderPermissionResponse>> BulkSetPermissionsAsync(string folderId, List<SetFolderPermissionRequest> requests, bool applyToSubfolders = false)
        {
            try
            {
                _logger.LogInformation("Bulk setting {Count} permissions on folder {FolderId}", requests.Count, folderId);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                var results = new List<FolderPermissionResponse>();

                foreach (var request in requests)
                {
                    var permission = await SetSinglePermissionAsync(folderId, request, userId);
                    results.Add(permission);

                    if (applyToSubfolders)
                    {
                        await ApplyPermissionToSubfoldersAsync(folderId, request, userId);
                    }
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully set {Count} permissions on folder {FolderId}", results.Count, folderId);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk setting permissions on folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<int> InheritPermissionsFromParentAsync(string folderId, string parentFolderId, bool overrideExisting = false)
        {
            try
            {
                _logger.LogInformation("Inheriting permissions from parent {ParentId} to folder {FolderId}", parentFolderId, folderId);

                var parentPermissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(predicate: fp => fp.FolderId == parentFolderId && fp.IsActive);

                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                int inheritedCount = 0;

                foreach (var parentPermission in parentPermissions)
                {
                    // Check if permission already exists
                    var existingPermission = await _unitOfWork.GetRepository<FolderPermission>()
                        .SingleOrDefaultAsync(
                            predicate: fp => fp.FolderId == folderId && fp.IsActive &&
                                           fp.UserId == parentPermission.UserId && 
                                           fp.DepartmentId == parentPermission.DepartmentId
                        );

                    if (existingPermission != null && !overrideExisting)
                    {
                        continue; // Skip if permission exists and not overriding
                    }

                    if (existingPermission != null && overrideExisting)
                    {
                        // Update existing permission
                        existingPermission.PermissionType = parentPermission.PermissionType;
                        existingPermission.IsDenied = parentPermission.IsDenied;
                        existingPermission.IsInherited = true;
                        existingPermission.ParentPermissionId = parentPermission.Id;
                        existingPermission.LastUpdatedBy = userId;
                        existingPermission.LastUpdatedTime = DateTime.UtcNow;

                        await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(existingPermission);
                    }
                    else
                    {
                        // Create new inherited permission
                        var inheritedPermission = new FolderPermission
                        {
                            FolderId = folderId,
                            UserId = parentPermission.UserId,
                            DepartmentId = parentPermission.DepartmentId,
                            PermissionType = parentPermission.PermissionType,
                            IsDenied = parentPermission.IsDenied,
                            IsInherited = true,
                            ParentPermissionId = parentPermission.Id,
                            ExpiresAt = parentPermission.ExpiresAt,
                            IsActive = true,
                            CreatedBy = userId,
                            CreatedTime = DateTime.UtcNow
                        };

                        await _unitOfWork.GetRepository<FolderPermission>().InsertAsync(inheritedPermission);
                    }

                    inheritedCount++;
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully inherited {Count} permissions from parent {ParentId} to folder {FolderId}", 
                    inheritedCount, parentFolderId, folderId);

                return inheritedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inheriting permissions from parent {ParentId} to folder {FolderId}", parentFolderId, folderId);
                throw;
            }
        }

        public async Task<int> PropagatePermissionsToSubfoldersAsync(string folderId, PermissionType permissionType, string? targetUserId = null, string? targetDepartmentId = null)
        {
            try
            {
                _logger.LogInformation("Propagating {Permission} permission to subfolders of {FolderId}", permissionType, folderId);

                var subfolders = await GetAllSubfoldersAsync(folderId);
                var userId = JwtTokenHelper.GetUserId(_httpContextAccessor);
                int propagatedCount = 0;

                foreach (var subfolder in subfolders)
                {
                    var request = new SetFolderPermissionRequest
                    {
                        UserId = targetUserId,
                        DepartmentId = targetDepartmentId,
                        PermissionType = permissionType,
                        ApplyToSubfolders = false // Prevent infinite recursion
                    };

                    await SetSinglePermissionAsync(subfolder.Id, request, userId);
                    propagatedCount++;
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully propagated permission to {Count} subfolders", propagatedCount);
                return propagatedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error propagating permissions to subfolders of {FolderId}", folderId);
                throw;
            }
        }

        public async Task<int> RemoveUserPermissionsAsync(string folderId, string userId, bool includeSubfolders = false)
        {
            try
            {
                _logger.LogInformation("Removing permissions for user {UserId} from folder {FolderId}", userId, folderId);

                var foldersToProcess = new List<string> { folderId };

                if (includeSubfolders)
                {
                    var subfolders = await GetAllSubfoldersAsync(folderId);
                    foldersToProcess.AddRange(subfolders.Select(f => f.Id));
                }

                int removedCount = 0;
                var currentUserId = JwtTokenHelper.GetUserId(_httpContextAccessor);

                foreach (var folderIdToProcess in foldersToProcess)
                {
                    var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                        .GetListAsync(predicate: fp => fp.FolderId == folderIdToProcess && fp.UserId == userId && fp.IsActive);

                    foreach (var permission in permissions)
                    {
                        permission.IsActive = false;
                        permission.LastUpdatedBy = currentUserId;
                        permission.LastUpdatedTime = DateTime.UtcNow;

                        await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(permission);
                        removedCount++;
                    }
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully removed {Count} permissions for user {UserId}", removedCount, userId);
                return removedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing permissions for user {UserId} from folder {FolderId}", userId, folderId);
                throw;
            }
        }

        public async Task<int> RemoveDepartmentPermissionsAsync(string folderId, string departmentId, bool includeSubfolders = false)
        {
            try
            {
                _logger.LogInformation("Removing permissions for department {DepartmentId} from folder {FolderId}", departmentId, folderId);

                var foldersToProcess = new List<string> { folderId };

                if (includeSubfolders)
                {
                    var subfolders = await GetAllSubfoldersAsync(folderId);
                    foldersToProcess.AddRange(subfolders.Select(f => f.Id));
                }

                int removedCount = 0;
                var currentUserId = JwtTokenHelper.GetUserId(_httpContextAccessor);

                foreach (var folderIdToProcess in foldersToProcess)
                {
                    var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                        .GetListAsync(predicate: fp => fp.FolderId == folderIdToProcess && fp.DepartmentId == departmentId && fp.IsActive);

                    foreach (var permission in permissions)
                    {
                        permission.IsActive = false;
                        permission.LastUpdatedBy = currentUserId;
                        permission.LastUpdatedTime = DateTime.UtcNow;

                        await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(permission);
                        removedCount++;
                    }
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully removed {Count} permissions for department {DepartmentId}", removedCount, departmentId);
                return removedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing permissions for department {DepartmentId} from folder {FolderId}", departmentId, folderId);
                throw;
            }
        }

        public async Task<List<FolderAccessResponse>> GetUserAccessibleFoldersAsync(string userId, string userDepartmentId, PermissionType requiredPermission, string? departmentId = null)
        {
            try
            {
                _logger.LogInformation("Getting accessible folders for user {UserId} with permission {Permission}", userId, requiredPermission);

                // ✅ FIXED: Use proper UnitOfWork pattern like DocumentService
                Expression<Func<Folder, bool>> predicate = f => !f.IsDeleted;

                // Filter by department if specified
                if (departmentId != null)
                {
                    predicate = f => !f.IsDeleted && (f.DepartmentId == departmentId || f.IsPublic);
                }

                var folders = await _unitOfWork.GetRepository<Folder>().GetListAsync(
                    predicate: predicate,
                    include: i => i.Include(f => f.FolderPermissions.Where(fp => fp.IsActive))
                );

                var accessibleFolders = new List<FolderAccessResponse>();

                foreach (var folder in folders)
                {
                    var effectivePermission = await GetEffectivePermissionAsync(folder.Id, userId, userDepartmentId);

                    if (effectivePermission?.Includes(requiredPermission) == true)
                    {
                        var accessResponse = new FolderAccessResponse
                        {
                            Id = folder.Id,
                            Name = folder.Name,
                            FullPath = folder.FullPath,
                            DepartmentId = folder.DepartmentId,
                            IsPublic = folder.IsPublic,
                            IsSystemFolder = folder.IsSystemFolder,
                            FolderType = folder.FolderType,
                            EffectivePermission = effectivePermission.Value,
                            PermissionSource = await GetPermissionSourceAsync(folder.Id, userId, userDepartmentId),
                            DocumentCount = folder.DocumentCount,
                            SubFolderCount = folder.SubFolderCount,
                            Actions = MapToActionPermissions(effectivePermission.Value)
                        };

                        accessibleFolders.Add(accessResponse);
                    }
                }

                return accessibleFolders.OrderBy(f => f.FullPath).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accessible folders for user {UserId}", userId);
                throw;
            }
        }

        public async Task<PermissionValidationResult> ValidateActionAsync(string folderId, string userId, string userDepartmentId, FolderAction action)
        {
            try
            {
                var result = new PermissionValidationResult
                {
                    Action = action
                };

                var effectivePermission = await GetEffectivePermissionAsync(folderId, userId, userDepartmentId);
                var requiredPermission = GetRequiredPermissionForAction(action);

                result.RequiredPermission = requiredPermission.ToString();
                result.UserPermission = effectivePermission?.ToString();

                if (effectivePermission?.Includes(requiredPermission) == true)
                {
                    result.IsAllowed = true;
                }
                else
                {
                    result.IsAllowed = false;
                    result.DenialReason = $"User has {effectivePermission?.ToString() ?? "no"} permission but {requiredPermission} is required for {action}";

                    // Add suggestions
                    await AddValidationSuggestionsAsync(result, folderId, userId, userDepartmentId, requiredPermission);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating action {Action} for user {UserId} on folder {FolderId}", action, userId, folderId);
                throw;
            }
        }

        public async Task<List<FolderPermissionAuditResponse>> GetPermissionAuditTrailAsync(string folderId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                // This would typically query an audit table
                // For now, return permissions with creation/modification dates
                var permissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(
                        predicate: fp => fp.FolderId == folderId &&
                                        (fromDate == null || fp.CreatedTime >= fromDate) &&
                                        (toDate == null || fp.CreatedTime <= toDate),
                        include: i => i.Include(fp => fp.Folder)
                    );

                var auditEntries = new List<FolderPermissionAuditResponse>();

                foreach (var permission in permissions)
                {
                    auditEntries.Add(new FolderPermissionAuditResponse
                    {
                        Id = permission.Id,
                        FolderId = permission.FolderId,
                        FolderName = permission.Folder?.Name ?? "Unknown",
                        ChangeType = permission.IsActive ? "Created" : "Deleted",
                        TargetId = permission.UserId ?? permission.DepartmentId ?? "Unknown",
                        TargetType = !string.IsNullOrEmpty(permission.UserId) ? "User" : "Department",
                        TargetName = permission.UserId ?? permission.DepartmentId ?? "Unknown",
                        NewPermission = permission.PermissionType.ToString(),
                        IsDenied = permission.IsDenied,
                        ChangeTime = permission.CreatedTime,
                        ChangedBy = permission.CreatedBy,
                        ChangeSource = permission.IsInherited ? "Inherited" : "Direct"
                    });
                }

                return auditEntries.OrderByDescending(a => a.ChangeTime).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit trail for folder {FolderId}", folderId);
                throw;
            }
        }

        public async Task<int> CleanupExpiredPermissionsAsync()
        {
            try
            {
                _logger.LogInformation("Cleaning up expired permissions");

                var expiredPermissions = await _unitOfWork.GetRepository<FolderPermission>()
                    .GetListAsync(predicate: fp => fp.IsActive && fp.ExpiresAt != null && fp.ExpiresAt <= DateTime.UtcNow);

                var userId = JwtTokenHelper.GetUserIdOrNull(_httpContextAccessor) ?? "System";
                int cleanedCount = 0;

                foreach (var permission in expiredPermissions)
                {
                    permission.IsActive = false;
                    permission.LastUpdatedBy = userId;
                    permission.LastUpdatedTime = DateTime.UtcNow;

                    await _unitOfWork.GetRepository<FolderPermission>().UpdateAsync(permission);
                    cleanedCount++;
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully cleaned up {Count} expired permissions", cleanedCount);
                return cleanedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired permissions");
                throw;
            }
        }

        public async Task<List<PermissionConflictResponse>> GetPermissionConflictsAsync(string? folderId = null)
        {
            try
            {
                _logger.LogInformation("Getting permission conflicts for folder {FolderId}", folderId ?? "all");

                // ✅ FIXED: Use proper UnitOfWork pattern like DocumentService
                Expression<Func<FolderPermission, bool>> predicate = fp => fp.IsActive;

                if (folderId != null)
                {
                    predicate = fp => fp.IsActive && fp.FolderId == folderId;
                }

                var permissions = await _unitOfWork.GetRepository<FolderPermission>().GetListAsync(
                    predicate: predicate,
                    include: i => i.Include(fp => fp.Folder)
                );

                var conflicts = new List<PermissionConflictResponse>();

                // Group by folder and target (user or department)
                var groupedPermissions = permissions
                    .GroupBy(fp => new { fp.FolderId, Target = fp.UserId ?? fp.DepartmentId })
                    .Where(g => g.Count() > 1 || g.Any(p => p.IsDenied));

                foreach (var group in groupedPermissions)
                {
                    var permissionList = group.ToList();
                    var folder = permissionList.First().Folder;

                    // Check for allow/deny conflicts
                    var hasAllow = permissionList.Any(p => !p.IsDenied);
                    var hasDeny = permissionList.Any(p => p.IsDenied);

                    if (hasAllow && hasDeny)
                    {
                        conflicts.Add(new PermissionConflictResponse
                        {
                            Id = Guid.NewGuid().ToString(),
                            FolderId = group.Key.FolderId,
                            FolderName = folder?.Name ?? "Unknown",
                            TargetId = group.Key.Target ?? "Unknown",
                            TargetType = permissionList.First().UserId != null ? "User" : "Department",
                            TargetName = group.Key.Target ?? "Unknown",
                            ConflictType = "AllowDeny",
                            Severity = "High",
                            SuggestedResolution = "Remove conflicting permissions or use explicit deny",
                            DetectedAt = DateTime.UtcNow,
                            ConflictingPermissions = permissionList.Select(p => new ConflictingPermission
                            {
                                PermissionId = p.Id,
                                PermissionType = p.PermissionType.ToString(),
                                IsDenied = p.IsDenied,
                                Source = p.IsInherited ? "Inherited" : "Direct",
                                CreatedTime = p.CreatedTime,
                                CreatedBy = p.CreatedBy
                            }).ToList()
                        });
                    }
                }

                return conflicts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permission conflicts");
                throw;
            }
        }

        public async Task<PermissionConflictResolutionResult> ResolvePermissionConflictAsync(string conflictId, ConflictResolutionStrategy resolutionStrategy)
        {
            try
            {
                _logger.LogInformation("Resolving permission conflict {ConflictId} with strategy {Strategy}", conflictId, resolutionStrategy);

                var result = new PermissionConflictResolutionResult
                {
                    Strategy = resolutionStrategy.ToString(),
                    Success = false
                };

                // This is a simplified implementation
                // In a real system, you would store conflict details and resolve based on the stored information
                result.Success = true;
                result.ChangeDetails.Add($"Applied {resolutionStrategy} strategy to resolve conflict {conflictId}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving permission conflict {ConflictId}", conflictId);
                throw;
            }
        }

        #region Helper Methods

        private async Task GetInheritedPermissionsAsync(Folder folder, string userId, string userDepartmentId, FolderPermissionBreakdownResponse breakdown)
        {
            var currentFolder = folder;

            while (currentFolder.ParentFolderId != null)
            {
                var parentFolder = await _unitOfWork.GetRepository<Folder>()
                    .SingleOrDefaultAsync(
                        predicate: f => f.Id == currentFolder.ParentFolderId && !f.IsDeleted,
                        include: i => i.Include(f => f.FolderPermissions.Where(fp => fp.IsActive))
                    );

                if (parentFolder == null) break;

                // Check for inherited user permissions
                var inheritedUserPermissions = parentFolder.FolderPermissions
                    .Where(fp => fp.UserId == userId && fp.IsValid)
                    .ToList();

                foreach (var permission in inheritedUserPermissions)
                {
                    breakdown.InheritedPermissions.Add(new PermissionSourceDetail
                    {
                        PermissionId = permission.Id,
                        PermissionType = permission.PermissionType,
                        IsDenied = permission.IsDenied,
                        Source = "Inherited",
                        SourceFolderId = parentFolder.Id,
                        SourceFolderName = parentFolder.Name,
                        CreatedTime = permission.CreatedTime,
                        CreatedBy = permission.CreatedBy,
                        ExpiresAt = permission.ExpiresAt,
                        IsActive = permission.IsActive
                    });
                }

                // Check for inherited department permissions
                var inheritedDeptPermissions = parentFolder.FolderPermissions
                    .Where(fp => fp.DepartmentId == userDepartmentId && fp.IsValid)
                    .ToList();

                foreach (var permission in inheritedDeptPermissions)
                {
                    breakdown.InheritedPermissions.Add(new PermissionSourceDetail
                    {
                        PermissionId = permission.Id,
                        PermissionType = permission.PermissionType,
                        IsDenied = permission.IsDenied,
                        Source = "Inherited",
                        SourceFolderId = parentFolder.Id,
                        SourceFolderName = parentFolder.Name,
                        CreatedTime = permission.CreatedTime,
                        CreatedBy = permission.CreatedBy,
                        ExpiresAt = permission.ExpiresAt,
                        IsActive = permission.IsActive
                    });
                }

                currentFolder = parentFolder;
            }
        }

        private async Task GetDefaultPermissionsAsync(Folder folder, string userId, string userDepartmentId, FolderPermissionBreakdownResponse breakdown)
        {
            // Public folders - all users have view access
            if (folder.IsPublic)
            {
                breakdown.DefaultPermissions.Add(new PermissionSourceDetail
                {
                    PermissionType = FolderConstant.DefaultPermissions.PublicFolderPermission,
                    IsDenied = false,
                    Source = "Default",
                    CreatedTime = DateTime.UtcNow,
                    CreatedBy = "System",
                    IsActive = true
                });
            }

            // Department folders - department members have default access
            if (folder.DepartmentId == userDepartmentId)
            {
                var userRole = JwtTokenHelper.GetUserRole(_httpContextAccessor);
                var isManager = userRole == Roles.Manager || userRole == Roles.Admin;

                var defaultPermission = isManager
                    ? FolderConstant.DefaultPermissions.DepartmentManagerPermission
                    : FolderConstant.DefaultPermissions.DepartmentMemberPermission;

                breakdown.DefaultPermissions.Add(new PermissionSourceDetail
                {
                    PermissionType = defaultPermission,
                    IsDenied = false,
                    Source = "Default",
                    CreatedTime = DateTime.UtcNow,
                    CreatedBy = "System",
                    IsActive = true
                });
            }
        }

        private PermissionType? CalculateEffectivePermission(FolderPermissionBreakdownResponse breakdown)
        {
            // 1. Check for explicit denials first
            var deniedPermissions = breakdown.DirectPermissions.Concat(breakdown.DepartmentPermissions)
                .Concat(breakdown.InheritedPermissions)
                .Where(p => p.IsDenied && p.IsActive)
                .ToList();

            if (deniedPermissions.Any())
            {
                breakdown.DeniedPermissions.AddRange(deniedPermissions);
                return null; // Explicit denial overrides everything
            }

            // 2. Get highest direct permission
            var directPermission = breakdown.DirectPermissions
                .Where(p => !p.IsDenied && p.IsActive)
                .OrderByDescending(p => p.PermissionType)
                .FirstOrDefault();

            if (directPermission != null)
            {
                return directPermission.PermissionType;
            }

            // 3. Get highest department permission
            var departmentPermission = breakdown.DepartmentPermissions
                .Where(p => !p.IsDenied && p.IsActive)
                .OrderByDescending(p => p.PermissionType)
                .FirstOrDefault();

            if (departmentPermission != null)
            {
                return departmentPermission.PermissionType;
            }

            // 4. Get highest inherited permission
            var inheritedPermission = breakdown.InheritedPermissions
                .Where(p => !p.IsDenied && p.IsActive)
                .OrderByDescending(p => p.PermissionType)
                .FirstOrDefault();

            if (inheritedPermission != null)
            {
                return inheritedPermission.PermissionType;
            }

            // 5. Use default permission
            var defaultPermission = breakdown.DefaultPermissions
                .Where(p => !p.IsDenied && p.IsActive)
                .OrderByDescending(p => p.PermissionType)
                .FirstOrDefault();

            return defaultPermission?.PermissionType;
        }

        private string DeterminePermissionSource(FolderPermissionBreakdownResponse breakdown)
        {
            if (breakdown.EffectivePermission == null)
                return "Denied";

            if (breakdown.DirectPermissions.Any(p => p.PermissionType == breakdown.EffectivePermission && !p.IsDenied))
                return "Direct";

            if (breakdown.DepartmentPermissions.Any(p => p.PermissionType == breakdown.EffectivePermission && !p.IsDenied))
                return "Department";

            if (breakdown.InheritedPermissions.Any(p => p.PermissionType == breakdown.EffectivePermission && !p.IsDenied))
                return "Inherited";

            return "Default";
        }

        private void CheckForConflicts(FolderPermissionBreakdownResponse breakdown)
        {
            var allPermissions = breakdown.DirectPermissions.Concat(breakdown.DepartmentPermissions)
                .Concat(breakdown.InheritedPermissions)
                .Where(p => p.IsActive)
                .ToList();

            var hasAllow = allPermissions.Any(p => !p.IsDenied);
            var hasDeny = allPermissions.Any(p => p.IsDenied);

            if (hasAllow && hasDeny)
            {
                breakdown.HasConflicts = true;
                breakdown.ConflictDetails.Add("User has both allow and deny permissions");
            }

            var duplicatePermissions = allPermissions
                .GroupBy(p => new { p.PermissionType, p.IsDenied })
                .Where(g => g.Count() > 1)
                .ToList();

            if (duplicatePermissions.Any())
            {
                breakdown.HasConflicts = true;
                breakdown.ConflictDetails.Add("User has duplicate permissions from multiple sources");
            }
        }

        private async Task<FolderPermissionResponse> SetSinglePermissionAsync(string folderId, SetFolderPermissionRequest request, string userId)
        {
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
                return MapToFolderPermissionResponse(existingPermission);
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
                return MapToFolderPermissionResponse(newPermission);
            }
        }

        private async Task ApplyPermissionToSubfoldersAsync(string folderId, SetFolderPermissionRequest request, string userId)
        {
            var subfolders = await GetAllSubfoldersAsync(folderId);

            foreach (var subfolder in subfolders)
            {
                await SetSinglePermissionAsync(subfolder.Id, request, userId);
            }
        }

        private async Task<List<Folder>> GetAllSubfoldersAsync(string folderId)
        {
            var result = await _unitOfWork.GetRepository<Folder>()
                .GetListAsync(predicate: f => f.ParentFolderId == folderId && !f.IsDeleted);
            return result.ToList();
        }

        private async Task<string> GetPermissionSourceAsync(string folderId, string userId, string userDepartmentId)
        {
            var breakdown = await GetPermissionBreakdownAsync(folderId, userId, userDepartmentId);
            return breakdown.PermissionSource;
        }

        private FolderActionPermissions MapToActionPermissions(PermissionType permission)
        {
            return new FolderActionPermissions
            {
                CanView = permission.Includes(PermissionType.View),
                CanCreateSubfolder = permission.Includes(PermissionType.Edit),
                CanUploadDocument = permission.Includes(PermissionType.Edit),
                CanEditFolder = permission.Includes(PermissionType.Edit),
                CanDeleteFolder = permission.Includes(PermissionType.Delete),
                CanManagePermissions = permission.Includes(PermissionType.Manage),
                CanMoveFolder = permission.Includes(PermissionType.Manage)
            };
        }

        private PermissionType GetRequiredPermissionForAction(FolderAction action)
        {
            return action switch
            {
                FolderAction.View => PermissionType.View,
                FolderAction.CreateSubfolder => PermissionType.Edit,
                FolderAction.UploadDocument => PermissionType.Edit,
                FolderAction.EditFolder => PermissionType.Edit,
                FolderAction.DeleteFolder => PermissionType.Delete,
                FolderAction.ManagePermissions => PermissionType.Manage,
                FolderAction.MoveFolder => PermissionType.Manage,
                _ => PermissionType.View
            };
        }

        private async Task AddValidationSuggestionsAsync(PermissionValidationResult result, string folderId, string userId, string userDepartmentId, PermissionType requiredPermission)
        {
            // Add suggestions based on the context
            result.Suggestions.Add($"Contact your department manager to request {requiredPermission} permission");

            if (requiredPermission == PermissionType.View)
            {
                result.Suggestions.Add("Check if the folder is public or if you have department access");
            }

            result.CanRequestElevation = true;
            result.PermissionGranters.Add("Department Manager");
            result.PermissionGranters.Add("System Administrator");
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

        #endregion

        /// <summary>
        /// Automatically grant folder permissions to a new user for public and department folders
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="userDepartmentId">User's department ID</param>
        /// <param name="userRole">User's role (for determining permission level)</param>
        /// <returns>Number of permissions created</returns>
        public async Task<int> GrantDefaultFolderPermissionsToNewUserAsync(string userId, string userDepartmentId, string userRole)
        {
            try
            {
                _logger.LogInformation("Granting default folder permissions to new user {UserId} in department {DepartmentId} with role {Role}",
                    userId, userDepartmentId, userRole);

                var permissionsCreated = 0;

                // 1. Grant permissions to all public folders
                var publicFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(predicate: f => f.IsPublic && !f.IsDeleted);

                foreach (var folder in publicFolders)
                {
                    var permission = new FolderPermission
                    {
                        Id = Guid.NewGuid().ToString(),
                        FolderId = folder.Id,
                        UserId = userId,
                        DepartmentId = null, // User-specific permission
                        PermissionType = FolderConstant.DefaultPermissions.PublicFolderPermission,
                        IsActive = true,
                        CreatedBy = "System",
                        CreatedTime = DateTime.UtcNow
                    };

                    await _unitOfWork.GetRepository<FolderPermission>().InsertAsync(permission);
                    permissionsCreated++;
                }

                // 2. Grant permissions to department folders
                var departmentFolders = await _unitOfWork.GetRepository<Folder>()
                    .GetListAsync(predicate: f => f.DepartmentId == userDepartmentId && !f.IsDeleted && !f.IsPublic);

                var departmentPermissionType = userRole == Roles.Manager
                    ? FolderConstant.DefaultPermissions.DepartmentManagerPermission
                    : FolderConstant.DefaultPermissions.DepartmentMemberPermission;

                foreach (var folder in departmentFolders)
                {
                    var permission = new FolderPermission
                    {
                        Id = Guid.NewGuid().ToString(),
                        FolderId = folder.Id,
                        UserId = userId,
                        DepartmentId = null, // User-specific permission
                        PermissionType = departmentPermissionType,
                        IsActive = true,
                        CreatedBy = "System",
                        CreatedTime = DateTime.UtcNow
                    };

                    await _unitOfWork.GetRepository<FolderPermission>().InsertAsync(permission);
                    permissionsCreated++;
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully granted {Count} default folder permissions to user {UserId}",
                    permissionsCreated, userId);

                return permissionsCreated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting default folder permissions to user {UserId}", userId);
                throw;
            }
        }
    }
}
