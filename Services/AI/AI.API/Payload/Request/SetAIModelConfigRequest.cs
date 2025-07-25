using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class SetAIModelConfigRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string ModelId { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 10)]
        public string ApiKey { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [Range(1, 8192)]
        public int MaxTokens { get; set; } = 2048;

        [Range(0.0, 2.0)]
        public double Temperature { get; set; } = 0.7;

        [Range(0.0, 1.0)]
        public double TopP { get; set; } = 0.9;

        public bool TestConnection { get; set; } = true;
    }
}
