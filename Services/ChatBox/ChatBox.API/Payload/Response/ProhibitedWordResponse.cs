namespace ChatBox.API.Payload.Response
{
    public class ProhibitedWordResponse
    {
        public string Id { get; set; }
        public string Word { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
    }
}
