using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class UpdateConfigurationRequest
    {
        [Required]
        [RegularExpression(@"^[A-Za-z0-9:_-]+$", ErrorMessage = "Key contains invalid characters")]
        public string Key { get; set; }

        [Required]
        public string Value { get; set; }

        public string Category { get; set; }

        [StringLength(500)]
        public string Description { get; set; }
    }
}
