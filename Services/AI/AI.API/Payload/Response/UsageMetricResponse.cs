namespace AI.API.Payload.Response
{
    public class UsageMetricResponse
    {
        public int Id { get; set; }
        public string RequestId { get; set; }
        public string? UserId { get; set; }

        public string SourceService { get; set; }
        public string ModelType { get; set; }
        public int TokensUsed { get; set; }
        public int ResponseTimeMs { get; set; }
        public string Status { get; set; }
        public string? ErrorMessage { get; set; }
        public decimal? EstimatedCost { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
