using System.ComponentModel.DataAnnotations;
using Document.Domain.Models;
using Document.Domain.Enums;

namespace Document.API.Payload.Request.Folder
{
    /// <summary>
    /// Request model for creating a new folder
    /// </summary>
    public class CreateFolderRequest
    {
        /// <summary>
        /// Folder name - must be unique within the parent folder
        /// </summary>
        [Required(ErrorMessage = "Folder name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Folder name must be between 1 and 100 characters")]
        public string Name { get; set; }

        /// <summary>
        /// Optional folder description
        /// </summary>
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        /// <summary>
        /// Parent folder ID - null for root level folders
        /// </summary>
        public string? ParentFolderId { get; set; }

        /// <summary>
        /// Department ID that owns this folder
        /// Required for department folders, null for public folders
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// Whether this folder should be public (accessible to all employees)
        /// </summary>
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// Initial permissions to set on the folder
        /// </summary>
        public List<InitialFolderPermission>? InitialPermissions { get; set; }
    }

    /// <summary>
    /// Initial permission to set when creating a folder
    /// </summary>
    public class InitialFolderPermission
    {
        /// <summary>
        /// User ID for user-specific permission (null for department permission)
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Department ID for department-wide permission (null for user permission)
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// Permission type to grant
        /// </summary>
        [Required]
        public PermissionType PermissionType { get; set; }

        /// <summary>
        /// Permission expiration date (null for permanent)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }
}
