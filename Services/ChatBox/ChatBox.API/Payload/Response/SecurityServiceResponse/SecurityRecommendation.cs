namespace ChatBox.API.Payload.Response.SecurityServiceResponse
{
    public class SecurityRecommendation
    {
        public string Action { get; set; } // allow, block, flag, moderate
        public string Reason { get; set; }
        public double Confidence { get; set; }
        public List<string> SuggestedActions { get; set; } = new();
        public bool RequiresHumanReview { get; set; }
    }
}
