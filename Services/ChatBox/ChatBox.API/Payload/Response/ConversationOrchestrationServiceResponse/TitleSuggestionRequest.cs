namespace ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse
{
    public class TitleSuggestionRequest
    {
        public string Content { get; set; }
        public int? MaxLength { get; set; }
        public string Language { get; set; }
        public string Style { get; set; }
    }
}
