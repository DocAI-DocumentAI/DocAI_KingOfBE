namespace AI.API.Payload.Request
{
    public class SwitchModelRequest
    {
        public string SessionId { get; set; }
        public string NewModelId { get; set; }
        public string UserId { get; set; }
        public bool ContinueConversation { get; set; } = true;
    }
}
