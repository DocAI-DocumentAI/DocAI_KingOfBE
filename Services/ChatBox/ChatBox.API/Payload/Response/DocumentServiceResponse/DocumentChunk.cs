namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentChunk
    {
        public string ChunkId { get; set; }
        public string Content { get; set; }
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
