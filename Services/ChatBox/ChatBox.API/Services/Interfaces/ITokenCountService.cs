using ChatBox.API.Services.Implement;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatBox.API.Services.Interfaces
{
    public interface ITokenCountService
    {
        int CountTokens(string text, string modelName = null);

        int CountTokens(string text);
        bool IsWithinLimit(string text, int? maxTokens = null);
        int GetMaxTokensForModel(string modelName);
        int EstimateContextTokens(ChatHistory chatHistory);
        bool IsContextWithinLimit(ChatHistory chatHistory, string modelName);

    }
}
