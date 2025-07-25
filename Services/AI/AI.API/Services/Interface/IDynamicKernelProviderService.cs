using AI.Domain.Models;
using Microsoft.SemanticKernel;

namespace AI.API.Services.Interface
{
    public interface IDynamicKernelProviderService
    {
        Task<Kernel> CreateKernelAsync(string modelId, string apiKey, AIProviderType providerType, string? endpoint = null);
        void ClearCache();
    }
}
