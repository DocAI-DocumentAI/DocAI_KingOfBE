namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    public class TokenBreakdown
    {
        /// <summary>
        /// Number of tokens in the user input/query
        /// </summary>
        public int InputTokens { get; set; }
        
        /// <summary>
        /// Number of tokens in the system prompt
        /// </summary>
        public int SystemPromptTokens { get; set; }
        
        /// <summary>
        /// Number of tokens in the conversation history
        /// </summary>
        public int HistoryTokens { get; set; }
        
        /// <summary>
        /// Total tokens from all input components
        /// </summary>
        public int TotalInputTokens { get; set; }
        
        /// <summary>
        /// Estimated number of tokens for the model's response
        /// </summary>
        public int EstimatedResponseTokens { get; set; }
        
        /// <summary>
        /// Total estimated tokens (input + response)
        /// </summary>
        public int TotalEstimatedTokens { get; set; }
        
        /// <summary>
        /// Safety buffer tokens
        /// </summary>
        public int SafetyBuffer { get; set; }
        
        /// <summary>
        /// Total tokens including buffer
        /// </summary>
        public int TotalWithBuffer { get; set; }
        
        /// <summary>
        /// When the estimation was performed
        /// </summary>
        public DateTime EstimationTimestamp { get; set; }
        
        /// <summary>
        /// Method used for estimation
        /// </summary>
        public string EstimationMethod { get; set; }
        
        /// <summary>
        /// Model recommendations based on token usage
        /// </summary>
        public List<ModelRecommendation> ModelRecommendations { get; set; } = new();
        
        /// <summary>
        /// Suggestions for optimizing token usage
        /// </summary>
        public List<string> OptimizationSuggestions { get; set; } = new();
        
        /// <summary>
        /// Additional metadata about the estimation
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        /// <summary>
        /// Estimated cost based on token usage and model pricing
        /// </summary>
        public decimal EstimatedCost { get; set; }
    }
}
