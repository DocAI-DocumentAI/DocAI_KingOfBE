namespace AI.API.Payload.Response
{
    public class ConfigurationResponse : BaseResponse
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ModelConfigurationResponse : BaseResponse
    {
        public int Id { get; set; }
        public string ModelType { get; set; }
        public string ModelName { get; set; }
        public string Endpoint { get; set; }
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public bool IsActive { get; set; }
        public string ProviderName { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ModelProviderResponse : BaseResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public bool IsActive { get; set; }
        public List<ModelConfigurationResponse> Models { get; set; }
    }
}
