namespace Document.API.Payload.Request
{
    public class ReviewDocumentRequest
    {
        public bool IsApproved { get; set; }
        public string? Comments { get; set; }
    }
}
