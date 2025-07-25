namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentSearchResponse
    {
        public List<DocumentSearchItem> Documents { get; set; } = new();
        public int TotalCount { get; set; }
        public string Query { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }
}
