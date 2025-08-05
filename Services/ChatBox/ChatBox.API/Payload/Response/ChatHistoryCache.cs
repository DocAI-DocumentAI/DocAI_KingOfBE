namespace ChatBox.API.Payload.Response
{
    public class ChatHistoryCache
    {
        public List<CachedChatMessage> Messages { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }
}
