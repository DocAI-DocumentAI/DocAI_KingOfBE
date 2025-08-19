using Document.Domain.Models;

using Document.Domain.Enums;

namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Summary response model for folder listings
    /// </summary>
    public class FolderSummaryResponse
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
        /// Folder description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Full path from root
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Folder level/depth
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Whether this is a system folder
        /// </summary>
        public bool IsSystemFolder { get; set; }

        /// <summary>
        /// Whether this is a public folder
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Folder type for system folders
        /// </summary>
        public FolderType? FolderType { get; set; }

        /// <summary>
        /// Number of direct child folders
        /// </summary>
        public int SubFolderCount { get; set; }

        /// <summary>
        /// Number of documents in this folder
        /// </summary>
        public int DocumentCount { get; set; }

        /// <summary>
        /// User's permission level on this folder
        /// </summary>
        public PermissionType? UserPermission { get; set; }

        /// <summary>
        /// Whether user can create subfolders
        /// </summary>
        public bool CanCreateSubfolders { get; set; }

        /// <summary>
        /// Whether user can upload documents
        /// </summary>
        public bool CanUploadDocuments { get; set; }

        /// <summary>
        /// Creation time
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Last modification time
        /// </summary>
        public DateTime? LastUpdatedTime { get; set; }

        /// <summary>
        /// Department ID (null for public folders)
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// User who created this folder
        /// </summary>
        public string? CreatedBy { get; set; }
    }
}
