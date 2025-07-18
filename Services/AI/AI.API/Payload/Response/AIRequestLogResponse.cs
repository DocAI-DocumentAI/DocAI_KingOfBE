namespace AI.API.Payload.Response
{
    public class AIRequestLogResponse
    {
        public string RequestId { get; set; }
        public string UserId { get; set; }
        public string ModelType { get; set; }
        public string Status { get; set; }
        public object RequestContent { get; set; }
        public object ResponseContent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
