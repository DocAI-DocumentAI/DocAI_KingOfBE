namespace AI.API.Payload.Response
{
    public class ModelSwitchResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? PreviousModel { get; set; }
        public string? NewModel { get; set; }
        public string SessionId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
