using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class UpdateModelConfigurationRequest
    {
        [Required]
        public string ModelType { get; set; }

        [Required]
        public string ModelName { get; set; }

        [Required]
        [Url(ErrorMessage = "Invalid endpoint URL")]
        public string Endpoint { get; set; }

        [Range(1, 8192)]
        public int MaxTokens { get; set; } = 2048;

        [Range(0, 2)]
        public double Temperature { get; set; } = 0.7;

        [Range(0, 1)]
        public double TopP { get; set; } = 0.9;

        public bool IsActive { get; set; }
    }
}
