using System.ComponentModel.DataAnnotations;

namespace ChatBox.API.Payload.Request.ChatService
{
    public class SearchRequest
    {
        public string Query { get; set; }
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 20;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<Guid> SessionIds { get; set; } = new();
    }
}
