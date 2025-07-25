namespace ChatBox.API.Payload.Response.AIServiceResponse
{
    public class StreamingChunk
    {
        public string StreamId { get; set; }
        public string Content { get; set; }
        public int ChunkIndex { get; set; }
        public bool IsComplete { get; set; }
        public string ChunkType { get; set; } = "text"; // text, citation, suggestion
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
