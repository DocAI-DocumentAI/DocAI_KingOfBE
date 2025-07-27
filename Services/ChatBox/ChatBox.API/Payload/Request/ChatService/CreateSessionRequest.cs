namespace ChatBox.API.Payload.Request.ChatService
{
    public class CreateSessionRequest
    {
        public string Title { get; set; }
        public string InitialContext { get; set; }
        public string AIModelId { get; set; }
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 2000;
    }
}
