namespace ChatBox.API.Payload.Request.DocumentClientService
{
    public class DocumentSearchRequest
    {
        public string Query { get; set; }
        public Guid UserId { get; set; }
        public int MaxResults { get; set; } = 5;
        public bool IncludeContent { get; set; } = true;
        public bool FilterByAccess { get; set; } = true;
        public List<string> Categories { get; set; } = new();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
