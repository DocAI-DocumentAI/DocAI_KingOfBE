namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentStatus
    {
        public string DocumentId { get; set; }
        public bool IsActive { get; set; }
        public bool IsExpired { get; set; }
        public bool IsSuperseded { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string SupersededBy { get; set; }
        public string Status { get; set; }
    }
}
