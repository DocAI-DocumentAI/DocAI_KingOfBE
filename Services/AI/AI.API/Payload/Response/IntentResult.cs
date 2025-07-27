namespace AI.API.Payload.Response
{
    public class IntentResult
    {
        public bool Success { get; set; }
        public string DetectedIntent { get; set; }
        public double Confidence { get; set; }
        public List<IntentPrediction>? AlternativeIntents { get; set; }
        public string? Message { get; set; }
    }
    public class IntentPrediction
    {
        public string Intent { get; set; }
        public double Confidence { get; set; }
        public string? Description { get; set; }
    }
}
