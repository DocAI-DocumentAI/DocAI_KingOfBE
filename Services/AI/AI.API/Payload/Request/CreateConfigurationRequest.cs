using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class CreateConfigurationRequest
    {
        [Required]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        public string? Category { get; set; }

        public string? Description { get; set; }
    }
}
