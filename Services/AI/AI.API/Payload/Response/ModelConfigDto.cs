using AI.Domain.Models;

namespace AI.API.Payload.Response
{
    public class ModelConfigDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public AIProviderType ProviderType { get; set; }
        public string ModelId { get; set; }
        public string? Endpoint { get; set; }
        public string? OrganizationId { get; set; }
        public string? ApiVersion { get; set; }
        public string? Description { get; set; }
        public double? AverageResponseTime { get; set; }
        public bool IsEnabled { get; set; }
        public bool HasApiKey { get; set; }
        public bool IsTestedSuccessfully { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastTestedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public string? LastTestError { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
