namespace ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse
{
    public class ContentAnalysisRequest
    {
        public string Content { get; set; }
        public string AnalysisType { get; set; } = "safety";
        public Dictionary<string, object> Options { get; set; } = new();
    }
}
