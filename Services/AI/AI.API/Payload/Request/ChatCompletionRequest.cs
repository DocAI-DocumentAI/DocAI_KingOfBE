namespace AI.API.Payload.Request
{
    public class ChatCompletionRequest
    {
        public string SessionId { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
    }
}
