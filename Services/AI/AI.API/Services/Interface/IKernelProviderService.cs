using AI.Domain.Models;
using Microsoft.SemanticKernel.TextGeneration;

namespace AI.API.Services.Interface
{
    public interface IKernelProviderService
    {
        Task<ITextGenerationService> CreateTextGenerationServiceAsync(AIModelConfiguration config);

    }
}
