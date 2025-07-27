namespace AI.API.Payload.Response
{
    public class StreamChunk
    {
        public string Content { get; set; }
        public bool IsComplete { get; set; }
        public int? TokenCount { get; set; }
        public string RequestId { get; set; }
        public string? Error { get; set; }
        public bool HasContext { get; set; }
        public int DocumentsCount { get; set; }
    }
}
