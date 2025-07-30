namespace ChatBox.API.Services.Interfaces
{
    public interface ITokenCountService
    {
        int CountTokens(string text);
        bool IsWithinLimit(string text, int maxTokens = 4000);
    }
}
