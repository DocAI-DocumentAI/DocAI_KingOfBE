using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request.Folder
{
    /// <summary>
    /// Request model for moving a folder to a different parent
    /// </summary>
    public class MoveFolderRequest
    {
        /// <summary>
        /// New parent folder ID (null to move to root level)
        /// </summary>
        public string? NewParentFolderId { get; set; }

        /// <summary>
        /// Whether to preserve existing permissions or inherit from new parent
        /// </summary>
        public bool PreservePermissions { get; set; } = true;
    }
}
