using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request.Folder
{
    /// <summary>
    /// Request model for updating folder information
    /// </summary>
    public class UpdateFolderRequest
    {
        /// <summary>
        /// New folder name (optional - only update if provided)
        /// </summary>
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Folder name must be between 1 and 100 characters")]
        public string? Name { get; set; }

        /// <summary>
        /// New folder description (optional - only update if provided)
        /// </summary>
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }
    }
}
