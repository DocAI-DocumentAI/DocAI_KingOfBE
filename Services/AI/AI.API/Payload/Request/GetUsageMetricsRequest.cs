using AI.Domain.Enums;

namespace AI.API.Payload.Request
{
    public class GetUsageMetricsRequest
    {
        public string? UserId { get; set; }
        public string? SourceService { get; set; }
        public string? RequestId { get; set; }
        public ModelType? ModelType { get; set; }
        public RequestStatus? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 20;
        public string SortBy { get; set; } = "CreatedAt";
        public bool IsAscending { get; set; } = false;
    }
}
