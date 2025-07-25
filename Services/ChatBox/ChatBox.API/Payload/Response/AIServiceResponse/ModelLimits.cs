namespace ChatBox.API.Payload.Response
{
    public class ModelLimits
    {
        public int MaxTokens { get; set; }
        public int MaxInputTokens { get; set; }
        public int MaxOutputTokens { get; set; }
        public int MaxContextLength { get; set; }
        public double MaxTemperature { get; set; }
        public double MinTemperature { get; set; }
        public int RateLimitPerMinute { get; set; }
        public int RateLimitPerHour { get; set; }
        public int RateLimitPerDay { get; set; }
    }
}
