namespace AI.API.Payload.Request
{
    public class IntentDetectionRequest
    {
        public string Text { get; set; }
        public List<string>? PossibleIntents { get; set; }
    }
}
