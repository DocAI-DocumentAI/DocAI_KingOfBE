namespace ChatBox.API.Payload.Request.ChatService
{
    public class MessageFeedbackRequest
    {
        public int Rating { get; set; }
        public string Comment { get; set; }
        public string FeedbackType { get; set; }
    }
}
