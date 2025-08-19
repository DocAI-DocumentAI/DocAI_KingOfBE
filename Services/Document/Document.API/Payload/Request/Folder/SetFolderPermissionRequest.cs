using System.ComponentModel.DataAnnotations;
using Document.Domain.Models;
using Document.Domain.Enums;

namespace Document.API.Payload.Request.Folder
{
    /// <summary>
    /// Request model for setting folder permissions
    /// </summary>
    public class SetFolderPermissionRequest
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
        [Required(ErrorMessage = "Permission type is required")]
        public PermissionType PermissionType { get; set; }

        /// <summary>
        /// Whether to deny this permission explicitly
        /// </summary>
        public bool IsDenied { get; set; } = false;

        /// <summary>
        /// Permission expiration date (null for permanent)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Whether to apply this permission to all subfolders
        /// </summary>
        public bool ApplyToSubfolders { get; set; } = false;
    }
}
