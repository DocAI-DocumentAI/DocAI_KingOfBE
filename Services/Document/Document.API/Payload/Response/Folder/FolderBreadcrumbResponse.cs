namespace Document.API.Payload.Response.Folder
{
    /// <summary>
    /// Response model for folder breadcrumb navigation
    /// </summary>
    public class FolderBreadcrumbResponse
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
        /// Folder level in hierarchy
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Whether this is a system folder
        /// </summary>
        public bool IsSystemFolder { get; set; }

        /// <summary>
        /// Whether this is the current folder (last in breadcrumb)
        /// </summary>
        public bool IsCurrent { get; set; }
    }

    /// <summary>
    /// Validation result for folder operations
    /// </summary>
    public class FolderValidationResult
    {
        /// <summary>
        /// Whether the validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Validation error messages
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
