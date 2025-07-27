namespace AI.API.Payload.Request
{
    public class TokenCountRequest
    {
        public string Text { get; set; }
        public string? Model { get; set; }
    }
}
