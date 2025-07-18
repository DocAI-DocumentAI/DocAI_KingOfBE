namespace AI.API.Payload.Response
{
    public class BaseResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string RequestId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class BaseResponse<T> : BaseResponse
    {
        public T Data { get; set; }
    }

    public class PagedResponse<T> : BaseResponse
    {
        public List<T> Items { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public bool HasPrevious { get; set; }
        public bool HasNext { get; set; }
    }
}
