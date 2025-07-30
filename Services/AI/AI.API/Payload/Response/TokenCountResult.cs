namespace AI.API.Payload.Response
{
    public class TokenCountResult
    {
        public bool Success { get; set; }
        public string DetectedIntent { get; set; }
        public double Confidence { get; set; }
        public List<IntentPrediction>? AlternativeIntents { get; set; }
        public string? Message { get; set; }
    }
}
