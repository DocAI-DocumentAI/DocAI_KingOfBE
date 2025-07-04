using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces
{
    public interface IAIClient
    {
        Task<AIResponseExternal> GenerateAIResponseAsync(AIRequestExternal request);
        IAsyncEnumerable<string> StreamAIResponseAsync(AIRequestExternal request);
    }
}
