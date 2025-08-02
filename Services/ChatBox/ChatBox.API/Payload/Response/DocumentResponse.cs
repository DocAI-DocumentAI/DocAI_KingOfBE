namespace ChatBox.API.Payload.Response
{
    public class DocumentResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime ResponseTime { get; set; } = DateTime.UtcNow;

        public string Answer { get; set; } = string.Empty; 
        public List<DocumentSource> Sources { get; set; } = new(); 
        public string RawContext { get; set; } = string.Empty; 
        public double ConfidenceScore { get; set; } 
        public int TotalDocumentsSearched { get; set; }
    }
}
