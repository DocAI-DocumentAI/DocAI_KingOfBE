using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class UpdateAIModelConfigRequest
    {
        public string? Name { get; set; }
        public string? ModelId { get; set; }
        public string? ApiKey { get; set; }
        public string? Endpoint { get; set; }
        public string? OrganizationId { get; set; }
        public string? ApiVersion { get; set; }
        public string? Description { get; set; }
        public bool? IsEnabled { get; set; }
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public double? TopP { get; set; }
    }
}
