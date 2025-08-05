namespace Document.API.Configuration
{
    /// <summary>
    /// Configuration settings for Google Drive integration with personal Gmail accounts
    /// </summary>
    public class GoogleDriveConfiguration
    {
        public const string SectionName = "GoogleDrive";

        /// <summary>
        /// Company Gmail account that owns all folders (personal account)
        /// </summary>
        public string CompanyAccountEmail { get; set; } = "s3rcc.9@gmail.com";

        /// <summary>
        /// Google OAuth2 Client ID (from existing GoogleOAuth config)
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Google OAuth2 Client Secret (from existing GoogleOAuth config)
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Company root folder ID in Google Drive
        /// </summary>
        public string CompanyRootFolderId { get; set; }

        /// <summary>
        /// Application name for Google Drive API
        /// </summary>
        public string ApplicationName { get; set; } = "DocAI Document Management";

        /// <summary>
        /// Required scopes for Google Drive operations
        /// </summary>
        public string[] Scopes { get; set; } = new[]
        {
            "https://www.googleapis.com/auth/drive",
            "https://www.googleapis.com/auth/drive.file"
        };

        /// <summary>
        /// Timeout for Google Drive API operations (in seconds)
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Maximum retry attempts for failed operations
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Base delay for exponential backoff (in milliseconds)
        /// </summary>
        public int BaseDelayMs { get; set; } = 1000;

        /// <summary>
        /// Whether to enable detailed logging for Google Drive operations
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = true;

        /// <summary>
        /// Folder mapping for document workflow states
        /// </summary>
        public GoogleDriveFolderMapping FolderMapping { get; set; } = new();

        /// <summary>
        /// Whether to use company account for all write operations
        /// </summary>
        public bool UseCompanyAccountForWrites { get; set; } = true;

        /// <summary>
        /// Whether to automatically share files with users based on department
        /// </summary>
        public bool AutoShareWithDepartmentUsers { get; set; } = true;

        /// <summary>
        /// </summary>
        public string BaseUrl { get; set; } = "https://production.docai.asia";
    }

    /// <summary>
    /// Mapping of workflow states to Google Drive folder names
    /// </summary>
    public class GoogleDriveFolderMapping
    {
        public string Drafts { get; set; } = "drafts";
        public string Pending { get; set; } = "pending";
        public string Approved { get; set; } = "approved";
        public string Archived { get; set; } = "archived";
        public string Public { get; set; } = "public";
    }
}
