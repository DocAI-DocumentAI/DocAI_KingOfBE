using System.Security.Principal;

namespace ChatBox.API.Payload.Response.SecurityServiceResponse
{
    public class PIIDetectionResult
    {
        public bool ContainsPII { get; set; }
        public List<PIIEntity> DetectedPII { get; set; } = new();
        public double ConfidenceScore { get; set; }
        public string MaskedContent { get; set; }
        public List<string> PIITypes { get; set; } = new();
        public PIIRiskAssessment RiskAssessment { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
