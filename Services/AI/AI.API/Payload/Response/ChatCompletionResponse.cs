namespace AI.API.Payload.Response
{
    public class ChatCompletionResponse
    {
        public string SessionId { get; set; }
        public string Message { get; set; }
        public string Role { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
