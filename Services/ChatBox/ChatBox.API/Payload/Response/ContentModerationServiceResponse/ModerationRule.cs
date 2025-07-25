namespace ChatBox.API.Payload.Response.ContentModerationServiceResponse
{
    public class ModerationRule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string RuleType { get; set; } // keyword, pattern, ml_model, external_api
        public string Pattern { get; set; }
        public List<string> Keywords { get; set; } = new();
        public string Description { get; set; }
        public double Severity { get; set; }
        public string Action { get; set; } // block, flag, warn, log
        public bool IsActive { get; set; }
        public bool IsCaseSensitive { get; set; }
        public bool IsWholeWordOnly { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
