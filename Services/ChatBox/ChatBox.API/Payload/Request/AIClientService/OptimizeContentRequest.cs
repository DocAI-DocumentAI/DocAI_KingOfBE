using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.AIClientService
{
    /// <summary>
    /// Request model for optimizing content to fit within token limits
    /// </summary>
    public class OptimizeContentRequest
    {
        /// <summary>
        /// The content to optimize
        /// </summary>
        [Required]
        public string Content { get; set; }

        /// <summary>
        /// Maximum allowed tokens
        /// </summary>
        [Required]
        [Range(1, int.MaxValue)]
        public int MaxTokens { get; set; }

        /// <summary>
        /// Optimization strategy to apply
        /// </summary>
        /// <remarks>
        /// Available strategies:
        /// - intelligent: Uses a combination of optimization techniques (default)
        /// - conservative: Preserves as much content as possible
        /// - aggressive: Optimizes for maximum token reduction
        /// - simple: Simple truncation if other methods fail
        /// </remarks>
        public string OptimizationStrategy { get; set; } = "intelligent";
    }
} 