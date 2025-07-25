using System.ComponentModel.DataAnnotations;

namespace AI.Domain.Models
{
    public class AIModelConfiguration
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public AIProviderType ProviderType { get; set; }

        [Required]
        [MaxLength(200)]
        public string ModelId { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ApiKey { get; set; }

        [MaxLength(500)]
        public string? Endpoint { get; set; }

        [MaxLength(100)]
        public string? OrganizationId { get; set; }

        [MaxLength(50)]
        public string? ApiVersion { get; set; }

        public bool IsActive { get; set; } = false;

        public bool IsEnabled { get; set; } = true;

        public bool IsTestedSuccessfully { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastTestedAt { get; set; }

        [MaxLength(1000)]
        public string? LastTestError { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public double? AverageResponseTime { get; set; }

        public DateTime? LastUsedAt { get; set; }
    }
}
