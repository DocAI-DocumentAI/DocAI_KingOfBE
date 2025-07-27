namespace AI.API.Payload.Response
{
    public class AIModelConfigResponse 
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? ProviderType { get; set; }
        public string? ModelId { get; set; }
        public string? Endpoint { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsTestedSuccessfully { get; set; }
        public DateTime? LastTestedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
