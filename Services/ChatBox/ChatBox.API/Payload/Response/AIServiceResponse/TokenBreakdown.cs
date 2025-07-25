namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class TokenBreakdown
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
        public decimal EstimatedCost { get; set; }
    }
}
