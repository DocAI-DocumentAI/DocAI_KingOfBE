using ChatBox.API.Payload.Request.ConversationOrchestrationService;
using ChatBox.API.Payload.Response.ConversationOrchestrationServiceResponse;

namespace ChatBox.API.Services.Interfaces
{
    public interface IConversationOrchestrationService
    {
        Task<ConversationResponse> ProcessMessageAsync(ProcessMessageRequest request);
        Task<RAGResponse> ExecuteRAGWorkflowAsync(RAGRequest request);
    }
}
