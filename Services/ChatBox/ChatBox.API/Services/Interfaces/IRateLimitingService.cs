namespace ChatBox.API.Services.Interfaces
{
    public interface IRateLimitingService
    {
        Task RecordRequestAsync(Guid userId, string action);
        Task<bool> IsWithinLimitAsync(Guid userId, string action);
        Task ResetLimitsAsync(Guid userId);
    }
}
