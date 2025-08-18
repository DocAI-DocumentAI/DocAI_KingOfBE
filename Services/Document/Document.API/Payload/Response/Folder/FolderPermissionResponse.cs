using Document.Domain.Models;

using Document.Domain.Enums;

namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Response model for folder permissions
    /// </summary>
    public class FolderPermissionResponse
    {
        /// <summary>
        /// Permission ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Folder ID
        /// </summary>
        public string FolderId { get; set; }

        /// <summary>
        /// User ID (null for department permissions)
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// User email (for display purposes)
        /// </summary>
        public string? UserEmail { get; set; }

        /// <summary>
        /// User full name (for display purposes)
        /// </summary>
        public string? UserFullName { get; set; }

        /// <summary>
        /// Department ID (null for user permissions)
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// Department name (for display purposes)
        /// </summary>
        public string? DepartmentName { get; set; }

        /// <summary>
        /// Permission type
        /// </summary>
        public PermissionType PermissionType { get; set; }

        /// <summary>
        /// Permission type description
        /// </summary>
        public string PermissionDescription { get; set; }

        /// <summary>
        /// Whether this permission is inherited from parent folder
        /// </summary>
        public bool IsInherited { get; set; }

        /// <summary>
        /// Whether this permission is explicitly denied
        /// </summary>
        public bool IsDenied { get; set; }

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
        /// Source of the permission (Direct, Inherited, Department)
        /// </summary>
        public string PermissionSource { get; set; }

        /// <summary>
        /// Creation time
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Created by user
        /// </summary>
        public string CreatedBy { get; set; }
    }
}
