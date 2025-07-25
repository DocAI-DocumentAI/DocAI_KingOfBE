namespace ChatBox.API.Payload.Response.SecurityServiceResponse
{
    public class PIIRiskAssessment
    {
        public string RiskLevel { get; set; } // low, medium, high, critical
        public List<string> RiskFactors { get; set; } = new();
        public List<string> ComplianceIssues { get; set; } = new();
        public bool RequiresDataProtection { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
    }
}
