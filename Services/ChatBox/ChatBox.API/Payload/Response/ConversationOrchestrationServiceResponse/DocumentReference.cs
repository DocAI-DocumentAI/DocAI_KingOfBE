namespace ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse
{
    public class DocumentReference
    {
        public string DocumentId { get; set; }
        public string Title { get; set; }
        public string Excerpt { get; set; }
        public string Url { get; set; }
        public DateTime LastModified { get; set; }
        public double RelevanceScore { get; set; }
        public string DocumentType { get; set; }
    }
}
