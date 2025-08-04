namespace ChatBox.API.Payload.Response
{
    public class ModelTestResponse
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public int ResponseTime { get; set; } // milliseconds
        public string TestMessage { get; set; }
        public string Response { get; set; }
        public DateTime TestTime { get; set; } = DateTime.UtcNow;
    }
}
