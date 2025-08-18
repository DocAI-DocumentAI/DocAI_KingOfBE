using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Document.Domain.Enums;

namespace Document.Domain.Models
{
    /// <summary>
    /// Represents a folder in the hierarchical document management system
    /// Supports nested folder structure similar to Google Drive
    /// </summary>
    public class Folder : BaseEntity
    {
        /// <summary>
        /// Folder name - must be unique within the same parent folder
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; }

        /// <summary>
        /// Optional folder description
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Department that owns this folder (null for public folders)
        /// </summary>
        public string? DepartmentId { get; set; }

        /// <summary>
        /// Parent folder ID - null for root folders
        /// </summary>
        public string? ParentFolderId { get; set; }

        /// <summary>
        /// Corresponding Google Drive folder ID
        /// </summary>
        [Required]
        public string GoogleDriveFolderId { get; set; }

        /// <summary>
        /// Indicates if this is a system folder (_approved, _draft, etc.)
        /// System folders cannot be deleted or renamed by users
        /// </summary>
        public bool IsSystemFolder { get; set; } = false;

        /// <summary>
        /// Indicates if this folder is accessible to all employees (public)
        /// </summary>
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// Folder depth level in the hierarchy (0 for root folders)
        /// </summary>
        public int Level { get; set; } = 0;

        /// <summary>
        /// Full path from root to this folder (e.g., "Department/SubFolder/ChildFolder")
        /// Computed property for easy querying and display
        /// </summary>
        [StringLength(1000)]
        public string FullPath { get; set; }

        /// <summary>
        /// Folder type for system folders (Draft, Pending, Approved, Archived)
        /// Null for custom user-created folders
        /// </summary>
        public FolderType? FolderType { get; set; }

        /// <summary>
        /// Indicates if the folder is soft deleted
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Number of direct child folders
        /// </summary>
        public int SubFolderCount { get; set; } = 0;

        /// <summary>
        /// Number of documents directly in this folder
        /// </summary>
        public int DocumentCount { get; set; } = 0;

        // Navigation Properties

        /// <summary>
        /// Parent folder navigation property
        /// </summary>
        [ForeignKey(nameof(ParentFolderId))]
        public virtual Folder? ParentFolder { get; set; }

        /// <summary>
        /// Child folders collection
        /// </summary>
        public virtual ICollection<Folder> SubFolders { get; set; } = new List<Folder>();

        /// <summary>
        /// Documents directly in this folder
        /// </summary>
        public virtual ICollection<DocumentVersion> Documents { get; set; } = new List<DocumentVersion>();

        /// <summary>
        /// Folder-specific permissions
        /// </summary>
        public virtual ICollection<FolderPermission> FolderPermissions { get; set; } = new List<FolderPermission>();

        // Helper Methods

        /// <summary>
        /// Check if this folder is a root folder (no parent)
        /// </summary>
        public bool IsRootFolder => string.IsNullOrEmpty(ParentFolderId);

        /// <summary>
        /// Check if this folder can be deleted (not system folder, no documents, no subfolders)
        /// </summary>
        public bool CanBeDeleted => !IsSystemFolder && DocumentCount == 0 && SubFolderCount == 0;

        /// <summary>
        /// Get the folder hierarchy path as array
        /// </summary>
        public string[] GetPathArray()
        {
            return FullPath?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        }

        /// <summary>
        /// Check if this folder is an ancestor of the specified folder
        /// </summary>
        public bool IsAncestorOf(Folder folder)
        {
            if (folder == null) return false;
            return folder.FullPath?.StartsWith(FullPath + "/") == true;
        }

        /// <summary>
        /// Check if this folder is a descendant of the specified folder
        /// </summary>
        public bool IsDescendantOf(Folder folder)
        {
            if (folder == null) return false;
            return FullPath?.StartsWith(folder.FullPath + "/") == true;
        }
    }
}
