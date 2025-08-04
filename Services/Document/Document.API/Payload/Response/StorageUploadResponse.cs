namespace Document.API.Payload.Response
{
    public class StorageUploadResponse
    {
        public string FileIdentifier { get; set; }
        public string Md5Hash { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string ContentType { get; set; }
        public string StorageProvider { get; set; }
        public DateTime UploadedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
