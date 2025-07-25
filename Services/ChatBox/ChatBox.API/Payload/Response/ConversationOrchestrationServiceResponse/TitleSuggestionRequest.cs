namespace ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse
{
    public class TitleSuggestionRequest
    {

        public string Content { get; set; }
        public int MaxLength { get; set; } = 50;
    }
}
