namespace Document.API.Payload.Response
{
    /// <summary>
    /// Response model for Google Drive file upload operations
    /// Follows the same pattern as AzureUploadResponse
    /// </summary>
    public class GoogleDriveUploadResponse
    {
        public string FileId { get; set; }
        public string Md5Hash { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string ContentType { get; set; }
        public string FolderId { get; set; }
        public string DownloadUrl { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
