namespace Document.API.Payload.Request
{
    public class UploadDocumentRequest
    {
        public string Title { get; set; }
        public string? Description { get; set; }
        public IFormFile File { get; set; }
    }
}
