namespace AI.API.Payload.Response
{
    public class AIModelConfigResponse : BaseResponse
    {
        public string? ModelId { get; set; }
        public string? Description { get; set; }
        public string? Endpoint { get; set; }
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ConfiguredAt { get; set; }
        public DateTime? LastTestedAt { get; set; }
        public bool? LastTestResult { get; set; }
        public string? LastTestMessage { get; set; }
    }
}
