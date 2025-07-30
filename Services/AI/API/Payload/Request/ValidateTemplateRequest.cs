using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    /// <summary>
    /// Request to validate template syntax
    /// </summary>
    public class ValidateTemplateRequest
    {
        /// <summary>
        /// Template content to validate
        /// </summary>
        [Required]
        public string Template { get; set; }
        
        /// <summary>
        /// Sample variables to use in validation
        /// </summary>
        public Dictionary<string, string> Variables { get; set; }
    }
} 