using Document.Domain.Models;
using Document.Domain.Enums;

namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Response model for folder access information
    /// </summary>
    public class FolderAccessResponse
    {
        /// <summary>
        /// Folder ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Folder name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Full path from root
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Department ID that owns this folder
        /// </summary>
        public string DepartmentId { get; set; }

        /// <summary>
        /// Whether this is a public folder
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Whether this is a system folder
        /// </summary>
        public bool IsSystemFolder { get; set; }

        /// <summary>
        /// Folder type for system folders
        /// </summary>
        public FolderType? FolderType { get; set; }

        /// <summary>
        /// User's effective permission on this folder
        /// </summary>
        public PermissionType EffectivePermission { get; set; }

        /// <summary>
        /// Source of the permission (Direct, Inherited, Department, Default)
        /// </summary>
        public string PermissionSource { get; set; }

        /// <summary>
        /// Specific actions the user can perform
        /// </summary>
        public FolderActionPermissions Actions { get; set; } = new FolderActionPermissions();

        /// <summary>
        /// Number of documents in this folder
        /// </summary>
        public int DocumentCount { get; set; }

        /// <summary>
        /// Number of subfolders
        /// </summary>
        public int SubFolderCount { get; set; }

        /// <summary>
        /// Last access time for this user
        /// </summary>
        public DateTime? LastAccessTime { get; set; }
    }

    /// <summary>
    /// Specific action permissions for a folder
    /// </summary>
    public class FolderActionPermissions
    {
        /// <summary>
        /// Can view folder contents
        /// </summary>
        public bool CanView { get; set; }

        /// <summary>
        /// Can create subfolders
        /// </summary>
        public bool CanCreateSubfolder { get; set; }

        /// <summary>
        /// Can upload documents
        /// </summary>
        public bool CanUploadDocument { get; set; }

        /// <summary>
        /// Can edit folder properties
        /// </summary>
        public bool CanEditFolder { get; set; }

        /// <summary>
        /// Can delete folder
        /// </summary>
        public bool CanDeleteFolder { get; set; }

        /// <summary>
        /// Can manage folder permissions
        /// </summary>
        public bool CanManagePermissions { get; set; }

        /// <summary>
        /// Can move folder to different parent
        /// </summary>
        public bool CanMoveFolder { get; set; }
    }
}
