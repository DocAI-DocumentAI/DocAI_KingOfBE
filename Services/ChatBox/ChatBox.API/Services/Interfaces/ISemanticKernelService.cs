using ChatBox.API.Payload.Request;

using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;

namespace ChatBox.API.Services.Interfaces
{
    public interface ISemanticKernelService
    {
        Task<Kernel> GetKernelAsync(string modelName);
        Task<string> GetChatResponseAsync(string modelName, ChatHistory chatHistory);
        Task<IAsyncEnumerable<string>> GetChatResponseStreamAsync(string modelName, ChatHistory chatHistory);
        Task<ChatHistory> ReduceChatHistoryAsync(ChatHistory chatHistory);
        Task<string> GenerateTitleAsync(string message);
        Task<(bool Success, string Response, int TokensUsed, long ResponseTimeMs, string Error)> TestModelAsync(string modelName);

    }
}
