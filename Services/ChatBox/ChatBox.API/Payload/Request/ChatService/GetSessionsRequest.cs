namespace ChatBox.API.Payload.Request.ChatService
{
    public class GetSessionsRequest
    {
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 20;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Status { get; set; }
        public string SortBy { get; set; } = "LastActivityAt";
        public bool IsAscending { get; set; } = false;
    }
}
