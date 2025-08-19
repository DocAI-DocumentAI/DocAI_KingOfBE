using Document.Domain.Models;

using Document.Domain.Enums;

namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Detailed response model for a single folder
    /// </summary>
    public class FolderDetailResponse
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
        /// Department ID that owns this folder
        /// </summary>
        public string DepartmentId { get; set; }

        /// <summary>
        /// Parent folder ID
        /// </summary>
        public string? ParentFolderId { get; set; }

        /// <summary>
        /// Google Drive folder ID
        /// </summary>
        public string GoogleDriveFolderId { get; set; }

        /// <summary>
        /// Whether this is a system folder
        /// </summary>
        public bool IsSystemFolder { get; set; }

        /// <summary>
        /// Whether this is a public folder
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// Folder level/depth
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Full path from root
        /// </summary>
        public string FullPath { get; set; }

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
        /// Whether the folder can be deleted
        /// </summary>
        public bool CanBeDeleted { get; set; }

        /// <summary>
        /// User's effective permission on this folder
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
        /// Whether user can manage permissions
        /// </summary>
        public bool CanManagePermissions { get; set; }

        /// <summary>
        /// Parent folder information
        /// </summary>
        public FolderSummaryResponse? ParentFolder { get; set; }

        /// <summary>
        /// Direct child folders
        /// </summary>
        public List<FolderSummaryResponse> SubFolders { get; set; } = new List<FolderSummaryResponse>();

        /// <summary>
        /// Folder permissions
        /// </summary>
        public List<FolderPermissionResponse> Permissions { get; set; } = new List<FolderPermissionResponse>();

        /// <summary>
        /// Creation information
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// Last modification information
        /// </summary>
        public DateTime? LastUpdatedTime { get; set; }

        /// <summary>
        /// Created by user
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// Last updated by user
        /// </summary>
        public string? LastUpdatedBy { get; set; }
    }
}
