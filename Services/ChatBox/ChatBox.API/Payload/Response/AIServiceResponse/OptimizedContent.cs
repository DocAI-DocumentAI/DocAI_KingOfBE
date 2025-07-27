namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    /// <summary>
    /// Represents content that has been optimized for token limits
    /// </summary>
    public class OptimizedContent
    {
        /// <summary>
        /// The optimized text content
        /// </summary>
        public string OptimizedText { get; set; }
        
        /// <summary>
        /// Original token count before optimization
        /// </summary>
        public int OriginalTokenCount { get; set; }
        
        /// <summary>
        /// Token count after optimization
        /// </summary>
        public int OptimizedTokenCount { get; set; }
        
        /// <summary>
        /// Number of tokens saved through optimization
        /// </summary>
        public int TokensSaved { get; set; }
        
        /// <summary>
        /// Description of the optimization techniques applied
        /// </summary>
        public string OptimizationApplied { get; set; }
        
        /// <summary>
        /// Whether any optimization was performed
        /// </summary>
        public bool WasOptimized { get; set; }
    }
} 