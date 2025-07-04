namespace ChatBox.API.Payload.Response
{
    public class ChatResponse
    {
        public string ConversationId { get; set; }
        public string Answer { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
