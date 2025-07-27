using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.AIClientService
{
    /// <summary>
    /// Request model for validating if content is within token limits
    /// </summary>
    public class ValidateTokenLimitRequest
    {
        /// <summary>
        /// The content to validate
        /// </summary>
        [Required]
        public string Content { get; set; }

        /// <summary>
        /// Maximum allowed tokens
        /// </summary>
        [Required]
        [Range(1, int.MaxValue)]
        public int MaxTokens { get; set; }
    }
} 