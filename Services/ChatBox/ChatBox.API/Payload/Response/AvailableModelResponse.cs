namespace ChatBox.API.Payload.Response
{
    public class AvailableModelResponse
    {
        public string ModelName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int MaxTokens { get; set; }
        public bool IsDefault { get; set; }
        public bool IsFree { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public string Provider { get; set; }
    }
}
