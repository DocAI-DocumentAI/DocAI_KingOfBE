namespace ChatBox.API.Payload.Request.DocumentClientService
{
    public class BatchDocumentRequest
    {
        public List<string> DocumentIds { get; set; } = new();
        public Guid UserId { get; set; }
    }
}
