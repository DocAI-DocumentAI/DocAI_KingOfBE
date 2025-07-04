namespace ChatBox.API.Payload.Response
{
    public class ConversationSummaryResponse
    {
        public string Id { get; set; }
        public string? Title { get; set; }
        public DateTime LastActive { get; set; }
    }
}
