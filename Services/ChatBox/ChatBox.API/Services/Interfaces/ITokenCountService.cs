using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatBox.API.Services.Interfaces
{
    public interface ITokenCountService
    {
        int CountTokens(string text);
        bool IsWithinLimit(string text, int? maxTokens = null);
        bool IsContextWithinLimit(ChatHistory chatHistory, string modelName);

    }
}
