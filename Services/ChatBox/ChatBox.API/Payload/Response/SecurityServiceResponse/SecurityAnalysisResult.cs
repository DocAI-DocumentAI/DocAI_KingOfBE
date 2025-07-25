namespace ChatBox.API.Payload.Response.SecurityServiceResponse
{
    public class SecurityAnalysisResult
    {
        public bool HasSecurityIssues { get; set; }
        public double RiskScore { get; set; } // 0.0 to 1.0
        public List<SecurityThreat> DetectedThreats { get; set; } = new();
        public List<string> DetectedIssues { get; set; } = new();
        public SecurityRecommendation Recommendation { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime AnalysisTimestamp { get; set; }
        public string AnalysisId { get; set; }
    }
}
