using ChatBox.API.Payload.Response.ContentModerationServiceResponse;

namespace ChatBox.API.Services.Interfaces
{
    public interface IContentModerationService
    {
        Task<ContentModerationResponse> ModerateContentAsync(string content, Guid? userId);
        Task<bool> IsContentSafeAsync(string content);
        Task<List<string>> DetectProhibitedTermsAsync(string content);
        Task<bool> IsUserFlaggedAsync(Guid userId);
        Task UpdateModerationRulesAsync(List<ModerationRule> rules);
    }
}
