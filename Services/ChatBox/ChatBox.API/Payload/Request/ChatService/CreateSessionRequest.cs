namespace ChatBox.API.Payload.Request.ChatService
{
    public class CreateSessionRequest
    {
        public string Title { get; set; }
        public Dictionary<string, object> InitialContext { get; set; } = new();
        public string SessionType { get; set; } = "general";
    }
}
