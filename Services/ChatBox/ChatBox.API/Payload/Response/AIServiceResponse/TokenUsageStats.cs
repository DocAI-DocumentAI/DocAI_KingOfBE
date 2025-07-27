namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    /// <summary>
    /// Statistics about token usage for a user
    /// </summary>
    public class TokenUsageStats
    {
        /// <summary>
        /// The date for these statistics
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// Total number of tokens used on this date
        /// </summary>
        public int TotalTokensUsed { get; set; }
        
        /// <summary>
        /// Number of messages sent on this date
        /// </summary>
        public int MessageCount { get; set; }
        
        /// <summary>
        /// Average number of tokens per message
        /// </summary>
        public double AverageTokensPerMessage { get; set; }
        
        /// <summary>
        /// Maximum tokens used in a single message
        /// </summary>
        public int MaxTokensInSingleMessage { get; set; }
        
        /// <summary>
        /// Estimated cost of token usage
        /// </summary>
        public double EstimatedCost { get; set; }
    }
} 