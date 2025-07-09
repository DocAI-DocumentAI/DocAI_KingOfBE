namespace Document.API.Payload.Response
{
    public class BookmarkResponse
    {
        public string Id { get; set; }
        public string DocumentVersionId { get; set; }
        public string Title { get; set; }
        public string VersionName { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string FileType { get; set; }
        public DateTime CreatedTime { get; set; }
    }
}