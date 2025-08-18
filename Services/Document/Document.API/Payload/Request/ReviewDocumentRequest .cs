namespace Document.API.Payload.Request
{
    public class ReviewDocumentRequest
    {
        public bool IsApproved { get; set; }
        public string? Comments { get; set; }

        /// <summary>
        /// Target folder for approved documents (optional)
        /// If not specified, documents will be moved to default approved folder
        /// </summary>
        public string? TargetFolderId { get; set; }
    }
}
