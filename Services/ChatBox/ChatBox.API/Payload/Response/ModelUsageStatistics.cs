namespace ChatBox.API.Payload.Response
{
    public class ModelUsageStatistics
    {
        public string ModelName { get; set; }
        public int SessionCount { get; set; }
        public int MessageCount { get; set; }
        public long TokensUsed { get; set; }
        public int UniqueUsers { get; set; }
        public DateTime LastUsed { get; set; }
        public double AverageSessionLength { get; set; }
        public double UsagePercentage { get; set; }
    }
}
