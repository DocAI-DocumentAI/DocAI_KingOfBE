namespace Document.API.Payload.Request
{
    public record DocumentRAGRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string Query { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 5;
        public double? MinRelevanceScore { get; set; } = 0.7;
        public bool OnlyPublic { get; set; } = true;
        public bool OnlyOfficial { get; set; } = false;
        public string? DepartmentId { get; set; }
        public List<string>? Tags { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;
    }
}
