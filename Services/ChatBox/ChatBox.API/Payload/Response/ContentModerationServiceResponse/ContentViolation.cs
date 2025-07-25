namespace ChatBox.API.Payload.Response.ContentModerationServiceResponse
{
    public class ContentViolation
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string ViolationType { get; set; }
        public string Description { get; set; }
        public double Severity { get; set; }
        public string MatchedContent { get; set; }
        public int Position { get; set; }
        public int Length { get; set; }
        public List<string> SuggestedActions { get; set; } = new();
        public Dictionary<string, object> Context { get; set; } = new();
    }
}
