using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request
{
    public class AIConfigurationRequest
    {
        [Required]
        [StringLength(200)]
        public string ModelName { get; set; }

        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; }

        [Range(0.0, 2.0)]
        public double Temperature { get; set; } = 0.7;

        [Range(0.0, 1.0)]
        public double TopP { get; set; } = 1.0;

        [Range(1, 32000)]
        public int MaxTokens { get; set; } = 4000;

        public string SystemPrompt { get; set; }

        public bool IsFree { get; set; } = false;
    }
}   
