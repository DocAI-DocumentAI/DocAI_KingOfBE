namespace ChatBox.API.Payload.Response
{
    public class ConversationResponse
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string? Title { get; set; }
        public DateTime LastActive { get; set; }
        public List<MessageResponse> Messages { get; set; } = new List<MessageResponse>();
    }
}
