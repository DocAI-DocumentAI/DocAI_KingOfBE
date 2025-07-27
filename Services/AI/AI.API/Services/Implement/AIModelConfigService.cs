using AI.API.Services.Interface;
using AI.API.Services;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Interfaces;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using System.Diagnostics;

namespace AI.API.Services.Implement
{
    public class AIModelConfigService : IAIModelConfigService
    {
        private readonly IUnitOfWork<DocAIDbContext> _unitOfWork;
        private readonly IKernelProviderService _kernelProvider;
        private readonly ILogger<AIModelConfigService> _logger;

        public AIModelConfigService(
            IUnitOfWork<DocAIDbContext> unitOfWork,
            IKernelProviderService kernelProvider,
            ILogger<AIModelConfigService> logger)
        {
            _unitOfWork = unitOfWork;
            _kernelProvider = kernelProvider;
            _logger = logger;
        }

        public async Task<List<ModelConfigDto>> GetAllModelsAsync()
        {
            try
            {
                var repository = _unitOfWork.GetRepository<AIModelConfiguration>();
                var models = await repository.GetListAsync(
                    predicate: null,
                    orderBy: q => q.OrderBy(m => m.Id),
                    include: null);

                return models.Select(MapToResponse).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all models");
                throw;
            }
        }

        public async Task<ModelConfigDto?> GetModelByIdAsync(int id)
        {
            try
            {
                var repository = _unitOfWork.GetRepository<AIModelConfiguration>();
                var model = await repository.SingleOrDefaultAsync(
                    predicate: m => m.Id == id,
                    orderBy: null,
                    include: null);

                return model != null ? MapToResponse(model) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get model {ModelId}", id);
                throw;
            }
        }

        public async Task<bool> UpdateModelAsync(int id, UpdateAIModelConfigRequest request)
        {
            try
            {
                var repository = _unitOfWork.GetRepository<AIModelConfiguration>();
                var model = await repository.SingleOrDefaultAsync(
                    predicate: m => m.Id == id,
                    orderBy: null,
                    include: null);

                if (model == null)
                    return false;

                // Update model properties
                if (!string.IsNullOrEmpty(request.Name))
                    model.Name = request.Name;

                if (!string.IsNullOrEmpty(request.ModelId))
                    model.ModelId = request.ModelId;

                if (!string.IsNullOrEmpty(request.ApiKey))
                {
                    model.ApiKey = request.ApiKey;
                    // Reset test status when API key changes
                    model.IsTestedSuccessfully = false;
                    model.LastTestError = null;
                }

                if (!string.IsNullOrEmpty(request.Endpoint))
                    model.Endpoint = request.Endpoint;

                if (!string.IsNullOrEmpty(request.OrganizationId))
                    model.OrganizationId = request.OrganizationId;

                if (!string.IsNullOrEmpty(request.ApiVersion))
                    model.ApiVersion = request.ApiVersion;

                if (!string.IsNullOrEmpty(request.Description))
                    model.Description = request.Description;

                if (request.IsEnabled.HasValue)
                    model.IsEnabled = request.IsEnabled.Value;

                // Reset test status when configuration changes (especially API key)
                if (!string.IsNullOrEmpty(request.ApiKey) ||
                    !string.IsNullOrEmpty(request.Endpoint) ||
                    !string.IsNullOrEmpty(request.ModelId))
                {
                    model.IsTestedSuccessfully = false;
                    model.LastTestError = null;
                    model.LastTestedAt = null;
                }

                // Note: UpdatedAt property doesn't exist in domain model

                repository.UpdateAsync(model);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Updated model {ModelName} (ID: {ModelId})", model.Name, id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update model {ModelId}", id);
                throw;
            }
        }

        public async Task<TestModelResponse> TestModelAsync(int id)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var repository = _unitOfWork.GetRepository<AIModelConfiguration>();
                var model = await repository.SingleOrDefaultAsync(
                    predicate: m => m.Id == id,
                    orderBy: null,
                    include: null);

                if (model == null)
                {
                    return new TestModelResponse
                    {
                        Success = false,
                        Message = $"Model with ID {id} not found",
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                if (string.IsNullOrEmpty(model.ApiKey))
                {
                    return new TestModelResponse
                    {
                        Success = false,
                        Message = "API Key is required for testing",
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                try
                {
                    var textService = await _kernelProvider.CreateTextGenerationServiceAsync(model);
                    var result = await textService.GetTextContentsAsync("Hello, this is a test. Please respond with 'Test successful'.");
                    var response = result.FirstOrDefault()?.Text ?? "";

                    stopwatch.Stop();

                    // Update test results
                    model.IsTestedSuccessfully = true;
                    model.LastTestedAt = DateTime.UtcNow;
                    model.LastTestError = null;
                    repository.UpdateAsync(model);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Successfully tested model {ModelName} (ID: {ModelId})", model.Name, id);

                    return new TestModelResponse
                    {
                        Success = true,
                        Message = "Model tested successfully",
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        Response = response
                    };
                }
                catch (Exception testEx)
                {
                    stopwatch.Stop();

                    // Update test failure
                    model.IsTestedSuccessfully = false;
                    model.LastTestedAt = DateTime.UtcNow;
                    model.LastTestError = testEx.Message;
                    repository.UpdateAsync(model);
                    await _unitOfWork.CommitAsync();

                    _logger.LogWarning(testEx, "Failed to test model {ModelName} (ID: {ModelId})", model.Name, id);

                    return new TestModelResponse
                    {
                        Success = false,
                        Message = "Model test failed",
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        Error = testEx.Message
                    };
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to test model {ModelId}", id);

                return new TestModelResponse
                {
                    Success = false,
                    Message = "Internal error during model testing",
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Error = ex.Message
                };
            }
        }

        public async Task<bool> ActivateModelAsync(int id)
        {
            try
            {
                var repository = _unitOfWork.GetRepository<AIModelConfiguration>();
                var model = await repository.SingleOrDefaultAsync(
                    predicate: m => m.Id == id,
                    orderBy: null,
                    include: null);

                if (model == null)
                    return false;

                if (!model.IsTestedSuccessfully)
                    throw new InvalidOperationException("Model must be tested successfully before activation");

                // Deactivate all other models (exclude current model to avoid tracking conflict)
                var otherActiveModels = await repository.GetListAsync(
                    predicate: m => m.IsActive && m.Id != id,
                    orderBy: null,
                    include: null);

                foreach (var m in otherActiveModels)
                {
                    m.IsActive = false;
                    repository.UpdateAsync(m);
                }

                // Activate the selected model
                model.IsActive = true;
                repository.UpdateAsync(model);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Activated model {ModelName} (ID: {ModelId})", model.Name, id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to activate model {ModelId}", id);
                throw;
            }
        }

        public async Task<List<ProviderInfo>> GetSupportedProvidersAsync()
        {
            return await Task.FromResult(Enum.GetValues<AIProviderType>()
                .Select(p => new ProviderInfo
                {
                    Name = p.ToString(),
                    Value = (int)p,
                    DefaultEndpoint = GetDefaultEndpoint(p),
                    RequiresApiKey = true,
                    RequiresEndpoint = p == AIProviderType.AzureAIInference
                })
                .ToList());
        }

        private static ModelConfigDto MapToResponse(AIModelConfiguration model)
        {
            return new ModelConfigDto
            {
                Id = model.Id,
                Name = model.Name,
                ProviderType = model.ProviderType,
                ModelId = model.ModelId,
                Endpoint = model.Endpoint,
                OrganizationId = model.OrganizationId,
                ApiVersion = model.ApiVersion,
                Description = model.Description,
                AverageResponseTime = model.AverageResponseTime,
                IsEnabled = model.IsEnabled,
                HasApiKey = !string.IsNullOrEmpty(model.ApiKey),
                IsTestedSuccessfully = model.IsTestedSuccessfully,
                IsActive = model.IsActive,
                LastTestedAt = model.LastTestedAt,
                LastUsedAt = model.LastUsedAt,
                LastTestError = model.LastTestError,
                CreatedAt = model.CreatedAt,
                UpdatedAt = null // Domain model doesn't have UpdatedAt
            };
        }

        private static string GetDefaultEndpoint(AIProviderType providerType)
        {
            return providerType switch
            {
                AIProviderType.OpenAI => "https://api.openai.com/v1",
                AIProviderType.HuggingFace => "https://router.huggingface.co/v1/chat/completions",
                AIProviderType.MistralAI => "https://api.mistral.ai/v1",
                AIProviderType.GoogleGemini => "https://generativelanguage.googleapis.com/v1beta",
                AIProviderType.AzureAIInference => "https://your-endpoint.inference.ai.azure.com",
                _ => ""
            };
        }
    }
}
