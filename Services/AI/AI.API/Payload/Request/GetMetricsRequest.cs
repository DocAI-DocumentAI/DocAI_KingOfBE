using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class GetMetricsRequest
    {
        public string? UserId { get; set; }
        public string? SourceService { get; set; }
        public string? ModelType { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        [Range(1, 100)]
        public int Size { get; set; } = 20;

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        public string? SortBy { get; set; }
        public bool IsAscending { get; set; } = true;

        // Backward compatibility
        public int PageSize
        {
            get => Size;
            set => Size = value;
        }

        public int PageNumber
        {
            get => Page;
            set => Page = value;
        }
    }
}
