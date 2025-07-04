namespace ChatBox.API.Payload.Response
{
    public class MessageResponse
    {
        public string Id { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } // Thời gian của tin nhắn (từ CreateAt của MessageHistory)
        public int Order { get; set; } // REVIEW POINT: Thêm thuộc tính Order
    }
}
