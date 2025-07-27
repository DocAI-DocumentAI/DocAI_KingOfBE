namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class MessageFeedBackResponse
    {
        public int? Rating { get; set; }
        public string? Comment { get; set; }
        public string? FeedbackType { get; set; }
        public DateTime? FeedbackDate { get; set; }
    }
}
