namespace AI.API.Payload.Response
{
    public class ModelProviderResponse : BaseResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public bool IsActive { get; set; }
        public List<ModelConfigurationResponse> Models { get; set; }
    }
}
