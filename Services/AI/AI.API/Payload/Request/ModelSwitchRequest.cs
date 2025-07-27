namespace AI.API.Payload.Request
{
    public class ModelSwitchRequest
    {
        public string SessionId { get; set; }
        public string NewModelId { get; set; }
        public string UserId { get; set; }
        public bool ValidateOnly { get; set; } = false;
    }
}
