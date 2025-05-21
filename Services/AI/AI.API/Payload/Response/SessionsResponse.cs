namespace AI.API.Payload.Response
{
    public class SessionsResponse
    {
        public List<SessionInfo> Sessions { get; set; } = new List<SessionInfo>();
    }
    public class SessionInfo
    {
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string LastMessage { get; set; }
    }
}
