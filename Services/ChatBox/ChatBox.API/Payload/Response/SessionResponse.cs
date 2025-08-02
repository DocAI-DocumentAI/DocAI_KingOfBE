namespace ChatBox.API.Payload.Response
{
    public class SessionResponse
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ModelName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActiveAt { get; set; }
        public int MessageCount { get; set; }
    }
}
