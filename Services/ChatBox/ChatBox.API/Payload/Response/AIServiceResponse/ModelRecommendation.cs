namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    /// <summary>
    /// Represents a model recommendation based on token usage
    /// </summary>
    public class ModelRecommendation
    {
        /// <summary>
        /// Name of the AI model
        /// </summary>
        public string ModelName { get; set; }
        
        /// <summary>
        /// Maximum tokens the model can process
        /// </summary>
        public int MaxTokens { get; set; }
        
        /// <summary>
        /// Whether the model can accommodate the token count
        /// </summary>
        public bool CanAccommodate { get; set; }
        
        /// <summary>
        /// Percentage of the model's capacity that would be utilized
        /// </summary>
        public double UtilizationPercentage { get; set; }
        
        /// <summary>
        /// Score indicating how recommended this model is for the request
        /// </summary>
        public double RecommendationScore { get; set; }
        
        /// <summary>
        /// Estimated cost to use this model for the request
        /// </summary>
        public double EstimatedCost { get; set; }
    }
} 