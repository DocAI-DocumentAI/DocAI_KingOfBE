using ChatBox.Domain.Enum;

namespace ChatBox.API.Payload.Response.ContentModerationServiceResponse
{
    public class ContentModerationResponse
    {
        public bool IsApproved { get; set; }
        public string Reason { get; set; }
        public List<string> ViolatedRules { get; set; } = new();
        public double ConfidenceScore { get; set; }
        public string Action { get; set; } // approve, reject, flag, review
        public List<ContentViolation> Violations { get; set; } = new();
        public string ModerationId { get; set; }
        public DateTime ModerationTimestamp { get; set; }
        public ContentSeverity Severity { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
