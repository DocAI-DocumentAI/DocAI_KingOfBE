namespace ChatBox.API.Payload.Response
{
    public class ModelActivationResponse
    {
        public bool Success { get; set; }
        public string ModelName { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public string TestResponse { get; set; }
        public int ResponseTimeMs { get; set; }
        public bool WasAlreadyActive { get; set; }
    }
}
