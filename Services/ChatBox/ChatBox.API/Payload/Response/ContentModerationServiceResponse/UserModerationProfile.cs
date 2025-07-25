namespace ChatBox.API.Payload.Response.ContentModerationServiceResponse
{
    public class UserModerationProfile
    {
        public Guid UserId { get; set; }
        public int ViolationCount { get; set; }
        public DateTime? LastViolation { get; set; }
        public List<string> ViolationHistory { get; set; } = new();
        public bool IsFlagged { get; set; }
        public string FlagReason { get; set; }
        public DateTime? FlaggedUntil { get; set; }
        public double TrustScore { get; set; }
        public List<string> AllowedExceptions { get; set; } = new();
        public Dictionary<string, object> ModerationMetrics { get; set; } = new();
    }
}
