using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class GetMetricsRequest
    {
        public string UserId { get; set; }

        public string ModelType { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;
    }
}
