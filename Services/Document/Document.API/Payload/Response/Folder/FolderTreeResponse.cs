namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Response model for folder tree structure
    /// </summary>
    public class FolderTreeResponse
    {
        /// <summary>
        /// Root folder information
        /// </summary>
        public FolderNodeResponse RootFolder { get; set; }

        /// <summary>
        /// Total number of folders in the tree
        /// </summary>
        public int TotalFolders { get; set; }

        /// <summary>
        /// Maximum depth of the tree
        /// </summary>
        public int MaxDepth { get; set; }

        /// <summary>
        /// Whether system folders are included
        /// </summary>
        public bool IncludesSystemFolders { get; set; }

        /// <summary>
        /// Department ID (null for public folders)
        /// </summary>
        public string? DepartmentId { get; set; }
    }

    /// <summary>
    /// Individual folder node in the tree
    /// </summary>
    public class FolderNodeResponse
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
        public string? FolderType { get; set; }

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
        public string? UserPermission { get; set; }

        /// <summary>
        /// Whether user can create subfolders
        /// </summary>
        public bool CanCreateSubfolders { get; set; }

        /// <summary>
        /// Whether user can upload documents
        /// </summary>
        public bool CanUploadDocuments { get; set; }

        /// <summary>
        /// Child folders
        /// </summary>
        public List<FolderNodeResponse> SubFolders { get; set; } = new List<FolderNodeResponse>();

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
    }
}
