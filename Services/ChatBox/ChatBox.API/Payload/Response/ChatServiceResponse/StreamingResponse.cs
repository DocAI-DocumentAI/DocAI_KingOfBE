namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class StreamingResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Guid StreamId { get; set; }
        public Guid SessionId { get; set; }
        public string ConnectionId { get; set; }
        public DateTime StartedAt { get; set; }
    }
}
