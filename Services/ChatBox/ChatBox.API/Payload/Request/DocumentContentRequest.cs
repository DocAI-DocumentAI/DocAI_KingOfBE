namespace ChatBox.API.Payload.Request
{
    public class DocumentContentRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string DocumentId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }
}
