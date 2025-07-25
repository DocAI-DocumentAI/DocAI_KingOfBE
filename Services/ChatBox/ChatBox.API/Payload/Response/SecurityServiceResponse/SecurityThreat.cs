namespace ChatBox.API.Payload.Response.SecurityServiceResponse
{
    public class SecurityThreat
    {
        public string ThreatType { get; set; }
        public string Description { get; set; }
        public double Severity { get; set; } // 0.0 to 1.0
        public string Evidence { get; set; }
        public List<string> Mitigation { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
