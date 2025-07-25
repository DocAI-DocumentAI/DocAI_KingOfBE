namespace ChatBox.API.Payload.Response.ChatServiceResponse
{
    public class FeedbackInfo
    {
        public int? Rating { get; set; }
        public string Comment { get; set; }
        public DateTime? FeedbackDate { get; set; }
        public string FeedbackType { get; set; }
    }
}
