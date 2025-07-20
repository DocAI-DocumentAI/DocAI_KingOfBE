using AI.API.Payload.Request;
using AI.API.Payload.Response;

namespace AI.API.Services.Interface
{
    public interface IPromptTemplateService
    {
        Task<PromptTemplateResponse> GetTemplateAsync(string name);
        Task<PromptTemplateResponse> GetTemplateByIdAsync(int id);
        Task<List<PromptTemplateSummary>> GetAllTemplatesAsync(string category = null, bool activeOnly = true);
        Task<PromptTemplateResponse> CreateTemplateAsync(CreatePromptTemplateRequest request);
        Task<PromptTemplateResponse> UpdateTemplateAsync(int id, UpdatePromptTemplateRequest request);
        Task<bool> DeleteTemplateAsync(int id);
        Task<string> RenderTemplateAsync(string templateName, Dictionary<string, string> variables);
        Task<bool> ValidateTemplateAsync(string template, Dictionary<string, string> variables);
    }
}
