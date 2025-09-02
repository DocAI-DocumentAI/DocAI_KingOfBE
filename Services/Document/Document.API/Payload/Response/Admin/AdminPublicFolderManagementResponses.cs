using Document.Domain.Enums;

namespace Document.API.Payload.Response.Admin
{
    /// <summary>
    /// Response for manager public folder permission operations
    /// </summary>
    public class ManagerPublicFolderPermissionResponse
    {
        /// <summary>
        /// Permission ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Manager user ID
        /// </summary>
        public string ManagerUserId { get; set; } = string.Empty;

        /// <summary>
        /// Manager user name
        /// </summary>
        public string? ManagerUserName { get; set; }

        /// <summary>
        /// Manager user email
        /// </summary>
        public string? ManagerUserEmail { get; set; }

        /// <summary>
        /// Manager's department ID
        /// </summary>
        public string? ManagerDepartmentId { get; set; }

        /// <summary>
        /// Manager's department name
        /// </summary>
        public string? ManagerDepartmentName { get; set; }

        /// <summary>
        /// Public folder ID
        /// </summary>
        public string PublicFolderId { get; set; } = string.Empty;

        /// <summary>
        /// Public folder name
        /// </summary>
        public string? PublicFolderName { get; set; }

        /// <summary>
        /// Public folder full path
        /// </summary>
        public string? PublicFolderPath { get; set; }

        /// <summary>
        /// Permission type granted
        /// </summary>
        public PermissionType PermissionType { get; set; }

        /// <summary>
        /// Permission description
        /// </summary>
        public string PermissionDescription { get; set; } = string.Empty;

        /// <summary>
        /// Whether this permission is inherited from parent folder
        /// </summary>
        public bool IsInherited { get; set; }

        /// <summary>
        /// Permission expiration date
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Whether the permission is currently active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Whether the permission is currently valid (active and not expired)
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Who granted this permission
        /// </summary>
        public string? GrantedBy { get; set; }

        /// <summary>
        /// When the permission was granted
        /// </summary>
        public DateTime GrantedTime { get; set; }

        /// <summary>
        /// Last update time
        /// </summary>
        public DateTime? LastUpdatedTime { get; set; }

        /// <summary>
        /// Reason for granting permission
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Response for bulk permission operations
    /// </summary>
    public class BulkManagerPermissionOperationResponse
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Total number of managers processed
        /// </summary>
        public int TotalManagers { get; set; }

        /// <summary>
        /// Number of successful permission operations
        /// </summary>
        public int SuccessfulOperations { get; set; }

        /// <summary>
        /// Number of failed permission operations
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// List of processed permissions
        /// </summary>
        public List<ManagerPublicFolderPermissionResponse> ProcessedPermissions { get; set; } = new();

        /// <summary>
        /// List of operation errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Operation summary message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Details about the operation
        /// </summary>
        public Dictionary<string, object> OperationDetails { get; set; } = new();
    }

    /// <summary>
    /// Manager public folder access summary
    /// </summary>
    public class ManagerPublicFolderAccessSummary
    {
        /// <summary>
        /// Manager user ID
        /// </summary>
        public string ManagerUserId { get; set; } = string.Empty;

        /// <summary>
        /// Manager user name
        /// </summary>
        public string? ManagerUserName { get; set; }

        /// <summary>
        /// Manager user email
        /// </summary>
        public string? ManagerUserEmail { get; set; }

        /// <summary>
        /// Manager's department name
        /// </summary>
        public string? DepartmentName { get; set; }

        /// <summary>
        /// Total number of public folders the manager has access to
        /// </summary>
        public int TotalAccessibleFolders { get; set; }

        /// <summary>
        /// Number of folders with Edit permission
        /// </summary>
        public int EditPermissionFolders { get; set; }

        /// <summary>
        /// Number of folders with Delete permission
        /// </summary>
        public int DeletePermissionFolders { get; set; }

        /// <summary>
        /// Number of folders with Manage permission
        /// </summary>
        public int ManagePermissionFolders { get; set; }

        /// <summary>
        /// Highest permission level the manager has
        /// </summary>
        public PermissionType? HighestPermission { get; set; }

        /// <summary>
        /// List of specific folder permissions
        /// </summary>
        public List<ManagerPublicFolderPermissionResponse> FolderPermissions { get; set; } = new();

        /// <summary>
        /// When this summary was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Public folder manager access overview
    /// </summary>
    public class PublicFolderManagerAccessOverview
    {
        /// <summary>
        /// Public folder ID
        /// </summary>
        public string PublicFolderId { get; set; } = string.Empty;

        /// <summary>
        /// Public folder name
        /// </summary>
        public string? PublicFolderName { get; set; }

        /// <summary>
        /// Public folder path
        /// </summary>
        public string? PublicFolderPath { get; set; }

        /// <summary>
        /// Total number of managers with access
        /// </summary>
        public int TotalManagersWithAccess { get; set; }

        /// <summary>
        /// Number of managers with Edit permission
        /// </summary>
        public int ManagersWithEdit { get; set; }

        /// <summary>
        /// Number of managers with Delete permission
        /// </summary>
        public int ManagersWithDelete { get; set; }

        /// <summary>
        /// Number of managers with Manage permission
        /// </summary>
        public int ManagersWithManage { get; set; }

        /// <summary>
        /// List of managers with access
        /// </summary>
        public List<ManagerPublicFolderAccessSummary> ManagerAccess { get; set; } = new();

        /// <summary>
        /// Whether this folder inherits permissions from parent
        /// </summary>
        public bool HasInheritedPermissions { get; set; }

        /// <summary>
        /// Number of subfolders
        /// </summary>
        public int SubfolderCount { get; set; }
    }
}
