using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Document.Domain.Enums;

namespace Document.Domain.Models
{
    /// <summary>
    /// Represents permissions for a specific folder
    /// Supports both user-specific and department-wide permissions
    /// </summary>
    public class FolderPermission : BaseEntity
    {
        /// <summary>
        /// Folder that this permission applies to
        /// </summary>
        [Required]
        public string FolderId { get; set; }

        /// <summary>
        /// User ID for user-specific permissions
        /// Null for department-wide permissions
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Department ID for department-wide permissions
        /// Null for user-specific permissions
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// Type of permission granted (View, Edit, Delete)
        /// </summary>
        [Required]
        public PermissionType PermissionType { get; set; }

        /// <summary>
        /// Indicates if this permission is inherited from parent folder
        /// </summary>
        public bool IsInherited { get; set; } = false;

        /// <summary>
        /// Parent folder permission ID if this is inherited
        /// </summary>
        public string? ParentPermissionId { get; set; }

        /// <summary>
        /// Indicates if this permission is explicitly denied
        /// Explicit deny overrides inherited allow permissions
        /// </summary>
        public bool IsDenied { get; set; } = false;

        /// <summary>
        /// Permission expiration date (null for permanent permissions)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Indicates if the permission is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        // Navigation Properties

        /// <summary>
        /// Folder that this permission applies to
        /// </summary>
        [ForeignKey(nameof(FolderId))]
        public virtual Folder Folder { get; set; }

        /// <summary>
        /// Parent permission if this is inherited
        /// </summary>
        [ForeignKey(nameof(ParentPermissionId))]
        public virtual FolderPermission? ParentPermission { get; set; }

        /// <summary>
        /// Child permissions that inherit from this permission
        /// </summary>
        public virtual ICollection<FolderPermission> ChildPermissions { get; set; } = new List<FolderPermission>();

        // Helper Methods

        /// <summary>
        /// Check if this permission is currently valid (active and not expired)
        /// </summary>
        public bool IsValid => IsActive && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);

        /// <summary>
        /// Check if this is a user-specific permission
        /// </summary>
        public bool IsUserPermission => !string.IsNullOrEmpty(UserId);

        /// <summary>
        /// Check if this is a department-wide permission
        /// </summary>
        public bool IsDepartmentPermission => !string.IsNullOrEmpty(DepartmentId);

        /// <summary>
        /// Get the effective permission level considering denial
        /// </summary>
        public PermissionType? GetEffectivePermission()
        {
            if (!IsValid || IsDenied)
                return null;
            
            return PermissionType;
        }
    }
}
