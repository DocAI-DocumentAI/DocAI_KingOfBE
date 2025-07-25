namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    public class IntentDetectionResult
    {
        public string PredictedIntent { get; set; }
        public double Confidence { get; set; }
        public List<IntentScore> AllIntentScores { get; set; } = new();
        public Dictionary<string, object> ExtractedParameters { get; set; } = new();
        public bool RequiresClarification { get; set; }
        public List<string> ClarificationQuestions { get; set; } = new();
    }
}
