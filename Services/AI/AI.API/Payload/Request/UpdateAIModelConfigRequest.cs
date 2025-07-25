using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class UpdateAIModelConfigRequest
    {
        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(100)]
        public string? ModelId { get; set; }

        public string? ApiKey { get; set; }

        [StringLength(500)]
        public string? Endpoint { get; set; }

        [StringLength(100)]
        public string? OrganizationId { get; set; }

        [StringLength(50)]
        public string? ApiVersion { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        public bool? IsEnabled { get; set; }
    }
}
