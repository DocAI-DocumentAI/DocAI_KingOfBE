using Document.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request.Admin
{
    /// <summary>
    /// Request to grant manager permission to manage public folders
    /// </summary>
    public class GrantManagerPublicFolderPermissionRequest
    {
        /// <summary>
        /// Manager user ID to grant permission to
        /// </summary>
        [Required]
        public string ManagerUserId { get; set; }

        /// <summary>
        /// Specific public folder ID (optional - if null, applies to all public folders)
        /// </summary>
        public string? PublicFolderId { get; set; }

        /// <summary>
        /// Permission type to grant (Edit, Delete, or Manage)
        /// </summary>
        [Required]
        public PermissionType PermissionType { get; set; }

        /// <summary>
        /// Optional expiration date for the permission
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Apply to all existing subfolders as well
        /// </summary>
        public bool ApplyToSubfolders { get; set; } = true;

        /// <summary>
        /// Reason for granting permission (for audit trail)
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Request to revoke manager permission from public folders
    /// </summary>
    public class RevokeManagerPublicFolderPermissionRequest
    {
        /// <summary>
        /// Manager user ID to revoke permission from
        /// </summary>
        [Required]
        public string ManagerUserId { get; set; }

        /// <summary>
        /// Specific public folder ID (optional - if null, revokes from all public folders)
        /// </summary>
        public string? PublicFolderId { get; set; }

        /// <summary>
        /// Revoke from all existing subfolders as well
        /// </summary>
        public bool RevokeFromSubfolders { get; set; } = true;

        /// <summary>
        /// Reason for revoking permission (for audit trail)
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Request to get manager permissions for public folders
    /// </summary>
    public class GetManagerPublicFolderPermissionsRequest
    {
        /// <summary>
        /// Manager user ID (optional - if null, gets all manager permissions)
        /// </summary>
        public string? ManagerUserId { get; set; }

        /// <summary>
        /// Specific public folder ID (optional - if null, gets permissions for all public folders)
        /// </summary>
        public string? PublicFolderId { get; set; }

        /// <summary>
        /// Include expired permissions
        /// </summary>
        public bool IncludeExpired { get; set; } = false;

        /// <summary>
        /// Include inherited permissions
        /// </summary>
        public bool IncludeInherited { get; set; } = true;
    }

    /// <summary>
    /// Request to bulk grant permissions to multiple managers
    /// </summary>
    public class BulkGrantManagerPermissionsRequest
    {
        /// <summary>
        /// List of manager user IDs
        /// </summary>
        [Required]
        public List<string> ManagerUserIds { get; set; } = new();

        /// <summary>
        /// Specific public folder ID (optional - if null, applies to all public folders)
        /// </summary>
        public string? PublicFolderId { get; set; }

        /// <summary>
        /// Permission type to grant
        /// </summary>
        [Required]
        public PermissionType PermissionType { get; set; }

        /// <summary>
        /// Optional expiration date for the permissions
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Apply to all existing subfolders as well
        /// </summary>
        public bool ApplyToSubfolders { get; set; } = true;

        /// <summary>
        /// Reason for granting permissions (for audit trail)
        /// </summary>
        public string? Reason { get; set; }
    }
}
