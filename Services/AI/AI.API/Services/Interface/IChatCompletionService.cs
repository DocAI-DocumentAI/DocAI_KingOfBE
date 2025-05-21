namespace AI.API.Services.Interface
{
    public interface IChatCompletionService
    {
        Task<string> GetCompletionAsync(string sessionId, List<(string Role, string Content)> messages, Dictionary<string, object> settings = null);

    }
}
