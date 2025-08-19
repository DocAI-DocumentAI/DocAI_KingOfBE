using Document.Domain.Models;

using Document.Domain.Enums;

namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Detailed breakdown of how a user's permission on a folder is calculated
    /// </summary>
    public class FolderPermissionBreakdownResponse
    {
        /// <summary>
        /// Folder ID
        /// </summary>
        public string FolderId { get; set; }

        /// <summary>
        /// User ID
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// User's department ID
        /// </summary>
        public string UserDepartmentId { get; set; }

        /// <summary>
        /// Final effective permission
        /// </summary>
        public PermissionType? EffectivePermission { get; set; }

        /// <summary>
        /// How the effective permission was determined
        /// </summary>
        public string PermissionSource { get; set; }

        /// <summary>
        /// Direct permissions explicitly set for this user
        /// </summary>
        public List<PermissionSourceDetail> DirectPermissions { get; set; } = new List<PermissionSourceDetail>();

        /// <summary>
        /// Permissions inherited from parent folders
        /// </summary>
        public List<PermissionSourceDetail> InheritedPermissions { get; set; } = new List<PermissionSourceDetail>();

        /// <summary>
        /// Department-wide permissions
        /// </summary>
        public List<PermissionSourceDetail> DepartmentPermissions { get; set; } = new List<PermissionSourceDetail>();

        /// <summary>
        /// Default permissions based on folder type and user role
        /// </summary>
        public List<PermissionSourceDetail> DefaultPermissions { get; set; } = new List<PermissionSourceDetail>();

        /// <summary>
        /// Any denied permissions that override allows
        /// </summary>
        public List<PermissionSourceDetail> DeniedPermissions { get; set; } = new List<PermissionSourceDetail>();

        /// <summary>
        /// Whether there are any permission conflicts
        /// </summary>
        public bool HasConflicts { get; set; }

        /// <summary>
        /// Details of any permission conflicts
        /// </summary>
        public List<string> ConflictDetails { get; set; } = new List<string>();
    }

    /// <summary>
    /// Details of a permission source
    /// </summary>
    public class PermissionSourceDetail
    {
        /// <summary>
        /// Permission ID
        /// </summary>
        public string? PermissionId { get; set; }

        /// <summary>
        /// Permission type
        /// </summary>
        public PermissionType PermissionType { get; set; }

        /// <summary>
        /// Whether this permission is denied
        /// </summary>
        public bool IsDenied { get; set; }

        /// <summary>
        /// Source of the permission (Direct, Inherited, Department, Default)
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Source folder ID (for inherited permissions)
        /// </summary>
        public string? SourceFolderId { get; set; }

        /// <summary>
        /// Source folder name (for inherited permissions)
        /// </summary>
        public string? SourceFolderName { get; set; }

        /// <summary>
        /// When the permission was created
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Who created the permission
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// Permission expiration date
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Whether the permission is currently active
        /// </summary>
        public bool IsActive { get; set; }
    }
}
