using System.ComponentModel.DataAnnotations;
using AI.Domain.Models;

namespace AI.API.Payload.Request
{
    public class SetAIModelConfigRequest
    {
        public string Name { get; set; }
        public AIProviderType ProviderType { get; set; }
        public string ModelId { get; set; }
        public string ApiKey { get; set; }
        public string? Endpoint { get; set; }
        public string? OrganizationId { get; set; }
        public string? ApiVersion { get; set; }
        public string? Description { get; set; }
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public double? TopP { get; set; }
    }
}
