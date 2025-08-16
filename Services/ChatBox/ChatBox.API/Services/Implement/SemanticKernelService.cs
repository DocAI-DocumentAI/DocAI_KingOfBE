using ChatBox.API.Plugins;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using ChatBox.Infrastructure.Repository.Interfaces;
using ChatBox.API.Constants;
using System.Runtime.CompilerServices;

namespace ChatBox.API.Services.Implement
{
    public class SemanticKernelService : ISemanticKernelService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IDocumentSearchService _documentSearchService;
        private readonly ILogger<SemanticKernelService> _logger;


        public SemanticKernelService(
           IUnitOfWork<ChatBoxDbContext> unitOfWork,
           IConfiguration configuration,
           IDocumentSearchService documentSearchService,
           ILogger<SemanticKernelService> logger)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _documentSearchService = documentSearchService;
            _logger = logger;
        }

        public async Task<Kernel> GetKernelAsync(string modelName, bool requireActive = true)
        {
            var config = await GetAIConfigurationAsync(modelName, requireActive);

            if (config == null)
                throw new ArgumentException(string.Format(MessageConstant.Admin.ModelNotFound, modelName));

            return await CreateKernelForOpenRouterAsync(config);
        }
        public async Task<string> GetChatResponseAsync(string modelName, ChatHistory chatHistory, bool requireActive = true)
        {
            try
            {
                var kernel = await GetKernelAsync(modelName, requireActive); // ← ADD requireActive!
                var config = await GetAIConfigurationAsync(modelName, requireActive);
                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                var optimizedHistory = await OptimizeChatHistoryForModel(chatHistory, modelName);
                var executionSettings = CreateExecutionSettings(config, optimizedHistory);

                var result = await ExecuteChatCompletion(chatService, optimizedHistory, executionSettings);
                return ProcessChatResult(result, chatService, executionSettings, optimizedHistory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chat response for model {ModelName}", modelName);
                return MessageConstant.AI.ResponseGenerationFailed;
            }
        }
        public async Task<string> GetChatResponseAsync(string modelName, ChatHistory chatHistory)
        {
            return await GetChatResponseAsync(modelName, chatHistory, requireActive: true);
        }
        public async Task<Kernel> GetKernelAsync(string modelName)
        {
            return await GetKernelAsync(modelName, requireActive: true);
        }
        public async Task<IAsyncEnumerable<string>> GetChatResponseStreamAsync(string modelName, ChatHistory chatHistory)
        {
            var kernel = await GetKernelAsync(modelName, requireActive: true);
            var config = await GetAIConfigurationAsync(modelName);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var optimizedHistory = await OptimizeChatHistoryForModel(chatHistory, modelName);
            var executionSettings = CreateExecutionSettings(config, optimizedHistory);

            return StreamTokensAsync(chatService, optimizedHistory, executionSettings);
        }
        public async Task<ChatHistory> ReduceChatHistoryAsync(ChatHistory chatHistory)
        {
            if (chatHistory.Count <= ChatConstants.MaxChatHistoryCount)
                return chatHistory;

            return CreateReducedChatHistory(chatHistory);
        }
        public async Task<(bool Success, string Response, int TokensUsed, long ResponseTimeMs, string Error)> TestModelAsync(string modelName)
        {
            var startTime = DateTimeOffset.UtcNow;

            try
            {
                // ✅ LOG: Input model name
                _logger.LogInformation("SemanticKernel testing model: {ModelName}", modelName);

                var config = await ValidateModelForTesting(modelName);
                if (config.Error != null)
                {
                    _logger.LogError("Model validation failed: {Error}", config.Error);
                    return CreateTestFailureResult(startTime, config.Error);
                }

                // ✅ LOG: Model config found
                _logger.LogInformation("Model config found, proceeding with test");

                var testResult = await ExecuteModelTest(modelName, startTime);

                // ✅ LOG: Test result
                _logger.LogInformation("Model test result - Success: {Success}, Error: {Error}",
                    testResult.Success, testResult.Error);

                return testResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during model test: {ModelName}", modelName);
                return CreateTestExceptionResult(startTime, ex);
            }
        }
        public async Task<string> GenerateTitleAsync(string message)
        {
            try
            {
                var config = await GetDefaultAIConfigurationAsync();
                var kernel = await GetKernelAsync(config.ModelName);

                var titleFunction = kernel.CreateFunctionFromPrompt(ChatConstants.TitleGenerationPrompt);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var result = await titleFunction.InvokeAsync(kernel, new KernelArguments { ["input"] = message });

                return ProcessGeneratedTitle(result.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Title generation failed, using default title");
                return ChatConstants.DefaultSessionTitle;
            }
        }
        #region Private Methods - Kernel Management
        private async Task<Kernel> CreateKernelForOpenRouterAsync(AIConfiguration config)
        {
            try
            {
                var builder = Kernel.CreateBuilder();
                ConfigureOpenAIConnection(builder, config);

                var kernel = builder.Build();

                // Document search plugin integration - feature preserved but commented for future use
                // var documentPlugin = new DocumentSearchPlugin(_documentSearchService);
                // kernel.Plugins.AddFromObject(documentPlugin, "DocumentSearch");
                // kernel.Plugins.AddFromType<TimePlugin>("Time");

                return kernel;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    string.Format(MessageConstant.AI.KernelCreationFailed, config.ModelName), ex);
            }
        }

        private void ConfigureOpenAIConnection(IKernelBuilder builder, AIConfiguration config)
        {
            builder.AddOpenAIChatCompletion(
                modelId: config.ModelName,
                apiKey: _configuration["OpenRouter:APIKey"],
                endpoint: new Uri(_configuration["OpenRouter:Endpoint"]));
        }
        #endregion

        #region Private Methods - Chat History Optimization
        private async Task<ChatHistory> OptimizeChatHistoryForModel(ChatHistory chatHistory, string modelName)
        {
            var isMistralModel = IsMistralModel(modelName);

            if (isMistralModel && chatHistory.Count > ChatConstants.MistralMaxHistoryCount)
            {
                return await ReduceChatHistoryForMistralAsync(chatHistory);
            }

            return chatHistory;
        }

        private async Task<ChatHistory> ReduceChatHistoryForMistralAsync(ChatHistory original)
        {
            var reduced = new ChatHistory();

            PreserveSystemMessage(original, reduced);
            AddRecentNonSystemMessages(original, reduced);

            return reduced;
        }

        private void PreserveSystemMessage(ChatHistory source, ChatHistory destination)
        {
            var systemMsg = source.FirstOrDefault(m => m.Role == AuthorRole.System);
            if (systemMsg != null)
            {
                destination.Add(systemMsg);
            }
        }

        private void AddRecentNonSystemMessages(ChatHistory source, ChatHistory destination)
        {
            var nonSystemMessages = source.Where(m => m.Role != AuthorRole.System)
                           .TakeLast(ChatConstants.MistralKeepMessageCount);

            foreach (var msg in nonSystemMessages)
            {
                destination.Add(msg);
            }
        }

        private ChatHistory CreateReducedChatHistory(ChatHistory original)
        {
            var reduced = new ChatHistory();

            PreserveSystemMessage(original, reduced);

            var recentMessages = original.Where(m => m.Role != AuthorRole.System)
                           .TakeLast(ChatConstants.RecentMessagesCount);

            foreach (var message in recentMessages)
            {
                reduced.Add(message);
            }

            return reduced;
        }
        #endregion

        #region Private Methods - Execution Settings
        private OpenAIPromptExecutionSettings CreateExecutionSettings(AIConfiguration config, ChatHistory chatHistory)
        {
            var isMistralModel = IsMistralModel(config.ModelName);
            var hasSystemMessage = chatHistory.Any(m => m.Role == AuthorRole.System);

            return new OpenAIPromptExecutionSettings
            {
                ChatSystemPrompt = null,
                Temperature = isMistralModel ? Math.Min(config.Temperature, 0.8f) : config.Temperature,
                TopP = isMistralModel ? Math.Min(config.TopP, 0.9f) : config.TopP,
                MaxTokens = isMistralModel ? Math.Min(config.MaxTokens, 2000) : config.MaxTokens,
                ToolCallBehavior = null // Disabled for compatibility
            };
        }

        private bool IsMistralModel(string modelName)
        {
            return !string.IsNullOrEmpty(modelName) &&
                   modelName.ToLower().Contains("mistral");
        }
        #endregion

        #region Private Methods - Chat Completion
        private async Task<ChatMessageContent> ExecuteChatCompletion(
            IChatCompletionService chatService,
            ChatHistory chatHistory,
            OpenAIPromptExecutionSettings settings)
        {
            var kernelArguments = new KernelArguments();
            if (chatHistory.Any())
            {
                kernelArguments["userId"] = "system";
            }

            return await chatService.GetChatMessageContentAsync(chatHistory, settings);
        }

        private string ProcessChatResult(
            ChatMessageContent result,
            IChatCompletionService chatService,
            OpenAIPromptExecutionSettings settings,
            ChatHistory chatHistory)
        {
            if (!string.IsNullOrEmpty(result?.Content))
                return result.Content;

            _logger.LogWarning("Empty response from AI service, attempting fallback");
            return TryFallbackResponseAsync(chatService, settings, chatHistory).GetAwaiter().GetResult();
        }

        private async Task<string> TryFallbackResponseAsync(
            IChatCompletionService chatService,
            OpenAIPromptExecutionSettings settings,
            ChatHistory originalHistory)
        {
            try
            {
                var fallbackHistory = CreateFallbackChatHistory(originalHistory);
                var fallbackSettings = CreateFallbackSettings(settings);

                var result = await chatService.GetChatMessageContentAsync(fallbackHistory, fallbackSettings);
                return result?.Content ?? MessageConstant.AI.ResponseGenerationFailed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallback response generation also failed");
                return MessageConstant.AI.ResponseGenerationFailed;
            }
        }

        private ChatHistory CreateFallbackChatHistory(ChatHistory originalHistory)
        {
            var fallbackHistory = new ChatHistory();
            fallbackHistory.AddSystemMessage(ChatConstants.FallbackSystemPrompt);

            var lastUserMessage = originalHistory.LastOrDefault(m => m.Role == AuthorRole.User);
            var messageContent = lastUserMessage?.Content ?? ChatConstants.DefaultFallbackMessage;
            fallbackHistory.AddUserMessage(messageContent);

            return fallbackHistory;
        }

        private OpenAIPromptExecutionSettings CreateFallbackSettings(OpenAIPromptExecutionSettings originalSettings)
        {
            return new OpenAIPromptExecutionSettings
            {
                Temperature = originalSettings.Temperature,
                TopP = originalSettings.TopP,
                MaxTokens = Math.Min(originalSettings.MaxTokens ?? 1000, 500), // Reduced for fallback
                ToolCallBehavior = null // Disable tools for fallback
            };
        }
        #endregion

        #region Private Methods - Streaming
        private async IAsyncEnumerable<string> StreamTokensAsync(
            IChatCompletionService chatService,
            ChatHistory chatHistory,
            OpenAIPromptExecutionSettings executionSettings)
        {
            await foreach (var token in chatService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings))
            {
                if (!string.IsNullOrEmpty(token.Content))
                {
                    yield return token.Content;
                }
            }
        }
        #endregion

        #region Private Methods - Title Generation
        private string ProcessGeneratedTitle(string rawTitle)
        {
            var title = rawTitle.Trim().Replace("\"", "");

            return title.Length > ChatConstants.MaxTitleLength
                ? title.Substring(0, ChatConstants.MaxTitleLength) + "..."
                : title;
        }
        #endregion

        #region Private Methods - Model Testing
        private async Task<(AIConfiguration Config, string Error)> ValidateModelForTesting(string modelName)
        {
            var config = await _unitOfWork.GetRepository<AIConfiguration>()
         .SingleOrDefaultAsync(predicate: c => c.ModelName == modelName);

            if (config == null)
                return (null, string.Format(MessageConstant.Admin.ModelNotFound, modelName));

            //if (!config.IsActive)  // ❌ VẤN ĐỀ Ở ĐÂY!
            //    return (null, $"Model '{modelName}' chưa được kích hoạt");

            return (config, null);
        }

        private async Task<(bool Success, string Response, int TokensUsed, long ResponseTimeMs, string Error)> ExecuteModelTest(string modelName, DateTimeOffset startTime)
        {
            var testHistory = CreateTestChatHistory();
            var response = await GetChatResponseAsync(modelName, testHistory, requireActive: false);
            var endTime = DateTimeOffset.UtcNow;
            var responseTime = (long)(endTime - startTime).TotalMilliseconds;
            var tokensUsed = EstimateTokens(testHistory.ToString() + response);

            return (true, response, tokensUsed, responseTime, null);
        }

        private ChatHistory CreateTestChatHistory()
        {
            var testHistory = new ChatHistory();
            testHistory.AddSystemMessage(ChatConstants.TestSystemPrompt);
            testHistory.AddUserMessage(ChatConstants.TestUserMessage);
            return testHistory;
        }

        private (bool Success, string Response, int TokensUsed, long ResponseTimeMs, string Error) CreateTestFailureResult(DateTimeOffset startTime, string error)
        {
            var responseTime = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            return (false, "", 0, responseTime, error);
        }

        private (bool Success, string Response, int TokensUsed, long ResponseTimeMs, string Error) CreateTestExceptionResult(DateTimeOffset startTime, Exception ex)
        {
            var responseTime = (long)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            return (false, "", 0, responseTime, ex.Message);
        }

        private int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
        }
        #endregion

        #region Private Methods - Configuration Management
        private async Task<AIConfiguration> GetAIConfigurationAsync(string modelName, bool requireActive = true)
        {
            if (requireActive)
            {
                return await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.ModelName == modelName && c.IsActive);
            }
            else
            {
                return await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.ModelName == modelName);
            }
        }

        private async Task<AIConfiguration> GetDefaultAIConfigurationAsync()
        {
            var config = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.IsActive);

            return config ?? throw new InvalidOperationException(MessageConstant.Admin.NoActiveConfig);
        }
        #endregion
    }
}

