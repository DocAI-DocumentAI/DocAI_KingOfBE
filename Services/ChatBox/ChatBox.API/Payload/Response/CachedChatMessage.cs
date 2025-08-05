namespace ChatBox.API.Payload.Response
{
    public class CachedChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? AuthorName { get; set; }
    }
}
