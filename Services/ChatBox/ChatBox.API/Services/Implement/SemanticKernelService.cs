using ChatBox.API.Plugins;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using ChatBox.Infrastructure.Repository.Interfaces;
using ChatBox.API.Constants;

namespace ChatBox.API.Services.Implement
{
    public class SemanticKernelService : ISemanticKernelService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IDocumentSearchService _documentSearchService;

        public SemanticKernelService(
            IUnitOfWork<ChatBoxDbContext> unitOfWork,
            IConfiguration configuration,
            IDocumentSearchService documentSearchService)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _documentSearchService = documentSearchService;
        }

        public async Task<Kernel> GetKernelAsync(string modelName)
        {
            var config = await _unitOfWork.GetRepository<AIConfiguration>()
                           .SingleOrDefaultAsync(predicate: c => c.ModelName == modelName && c.IsActive);

            if (config == null)
                throw new ArgumentException(string.Format(MessageConstant.Admin.ModelNotFound, modelName));

            return await CreateKernelForOpenRouterAsync(config);
        }

        private async Task<Kernel> CreateKernelForOpenRouterAsync(AIConfiguration config)
        {
            var builder = Kernel.CreateBuilder();

            try
            {
                builder.AddOpenAIChatCompletion(
                    modelId: config.ModelName,
                    apiKey: _configuration["OpenRouter:APIKey"],
                    endpoint: new Uri(_configuration["OpenRouter:Endpoint"]));

                var kernel = builder.Build();

                //  DOCUMENT SEARCH PLUGIN INTEGRATION - FEATURE PRESERVED
                //var documentPlugin = new DocumentSearchPlugin(_documentSearchService);
                //kernel.Plugins.AddFromObject(documentPlugin, "DocumentSearch");
                //kernel.Plugins.AddFromType<TimePlugin>("Time");

                return kernel;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format(MessageConstant.AI.KernelCreationFailed, config.ModelName), ex);
            }
        }
        public async Task<string> GetChatResponseAsync(string modelName, ChatHistory chatHistory)
        {
            try
            {
                var kernel = await GetKernelAsync(modelName);
                var config = await GetAIConfigurationAsync(modelName);
                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                bool isMistralModel = IsMistralModel(modelName);
                if (isMistralModel && chatHistory.Count > ChatConstants.MistralMaxHistoryCount)
                {
                    chatHistory = await ReduceChatHistoryForMistralAsync(chatHistory);
                }

                bool hasSystemMessage = chatHistory.Any(m => m.Role == AuthorRole.System);
                var executionSettings = CreateExecutionSettings(config, hasSystemMessage, isMistralModel);

                // ADD USERID to kernel arguments for document search
                var kernelArguments = new KernelArguments();
                if (chatHistory.Any())
                {
                    kernelArguments["userId"] = "system"; 
                }

                var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings);

                if (string.IsNullOrEmpty(result?.Content))
                {
                    return await TryFallbackResponseAsync(chatService, executionSettings, chatHistory);
                }

                return result.Content;
            }
            catch (Exception ex)
            {
                return MessageConstant.AI.ResponseGenerationFailed;
            }
        }

        public async Task<IAsyncEnumerable<string>> GetChatResponseStreamAsync(string modelName, ChatHistory chatHistory)
        {
            var kernel = await GetKernelAsync(modelName);
            var config = await GetAIConfigurationAsync(modelName);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            bool isMistralModel = IsMistralModel(modelName);
            if (isMistralModel && chatHistory.Count > ChatConstants.MistralMaxHistoryCount)
            {
                chatHistory = await ReduceChatHistoryForMistralAsync(chatHistory);
            }

            bool hasSystemMessage = chatHistory.Any(m => m.Role == AuthorRole.System);
            var executionSettings = CreateExecutionSettings(config, hasSystemMessage, isMistralModel);

            return StreamTokensAsync(chatService, chatHistory, executionSettings);
        }

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

        public async Task<ChatHistory> ReduceChatHistoryAsync(ChatHistory chatHistory)
        {
            if (chatHistory.Count <= ChatConstants.MaxChatHistoryCount)
                return chatHistory;

            var reducedHistory = new ChatHistory();

            var systemMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
            if (systemMessage != null)
            {
                reducedHistory.Add(systemMessage);
            }

            var recentMessages = chatHistory.Skip(Math.Max(0, chatHistory.Count - ChatConstants.RecentMessagesCount)).ToList();

            foreach (var message in recentMessages)
            {
                if (message.Role != AuthorRole.System)
                {
                    reducedHistory.Add(message);
                }
            }

            return reducedHistory;
        }
        public async Task<string> GenerateTitleAsync(string message)
        {
            try
            {
                var config = await GetDefaultAIConfigurationAsync();
                var kernel = await GetKernelAsync(config.ModelName);

                var titleFunction = kernel.CreateFunctionFromPrompt(ChatConstants.TitleGenerationPrompt);

                var result = await titleFunction.InvokeAsync(kernel, new KernelArguments { ["input"] = message });
                var title = result.ToString().Trim().Replace("\"", "");

                // Limit title length
                return title.Length > ChatConstants.MaxTitleLength ? title.Substring(0, ChatConstants.MaxTitleLength) + "..." : title;
            }
            catch
            {
                return ChatConstants.DefaultSessionTitle;
            }
        }
        public async Task<(bool Success, string Response, int TokensUsed, long ResponseTimeMs, string Error)> TestModelAsync(string modelName)
        {
            var startTime = DateTimeOffset.UtcNow;

            try
            {
                var config = await _unitOfWork.GetRepository<AIConfiguration>()
                    .SingleOrDefaultAsync(predicate: c => c.ModelName == modelName);

                if (config == null)
                {
                    var endTime = DateTimeOffset.UtcNow;
                    var responseTime = (endTime - startTime).TotalMilliseconds;
                    return (false, "", 0, (long)responseTime, string.Format(MessageConstant.Admin.ModelNotFound, modelName));
                }

                if (!config.IsActive)
                {
                    var endTime = DateTimeOffset.UtcNow;
                    var responseTime = (endTime - startTime).TotalMilliseconds;
                    return (false, "", 0, (long)responseTime, $"Model '{modelName}' chưa được kích hoạt");
                }

                var testHistory = new ChatHistory();
                testHistory.AddSystemMessage(ChatConstants.TestSystemPrompt);
                testHistory.AddUserMessage(ChatConstants.TestUserMessage);

                var response = await GetChatResponseAsync(modelName, testHistory);
                var finalEndTime = DateTimeOffset.UtcNow;
                var finalResponseTime = (finalEndTime - startTime).TotalMilliseconds;

                var tokensUsed = EstimateTokens(testHistory.ToString() + response);

                return (true, response, tokensUsed, (long)finalResponseTime, null);
            }
            catch (Exception ex)
            {
                var endTime = DateTimeOffset.UtcNow;
                var responseTime = (endTime - startTime).TotalMilliseconds;
                return (false, "", 0, (long)responseTime, ex.Message);
            }
        }
        private int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
        }

        //  MISTRAL-SPECIFIC OPTIMIZATION METHODS
        private bool IsMistralModel(string modelName)
        {
            return !string.IsNullOrEmpty(modelName) &&
                   modelName.ToLower().Contains("mistral");
        }
        private OpenAIPromptExecutionSettings CreateExecutionSettings(AIConfiguration config, bool hasSystemMessageInHistory, bool isMistralModel = false)
        {
            var settings = new OpenAIPromptExecutionSettings
            {
                ChatSystemPrompt = null,
                Temperature = isMistralModel ? Math.Min(config.Temperature, 0.8f) : config.Temperature,
                TopP = isMistralModel ? Math.Min(config.TopP, 0.9f) : config.TopP,
                MaxTokens = isMistralModel ? Math.Min(config.MaxTokens, 2000) : config.MaxTokens,
            };

            //  DISABLE tool calling cho Mistral, enable cho models khác
            settings.ToolCallBehavior = null;
            //    ToolCallBehavior.AutoInvokeKernelFunctions;

            //if (isMistralModel)
            //{
            //    Console.WriteLine($"🔧 [MISTRAL] Function calling enabled via OpenRouter (format handled by proxy)");
            //}
            //else
            //{
            //    Console.WriteLine($"🔧 [TOOLS] Function calling enabled");
            //}
            return settings;
        }

        private async Task<ChatHistory> ReduceChatHistoryForMistralAsync(ChatHistory original)
        {
            var reduced = new ChatHistory();

            var systemMsg = original.FirstOrDefault(m => m.Role == AuthorRole.System);
            if (systemMsg != null)
            {
                reduced.Add(systemMsg);
            }

            var nonSystemMessages = original.Where(m => m.Role != AuthorRole.System)
                           .TakeLast(ChatConstants.MistralKeepMessageCount);
            foreach (var msg in nonSystemMessages)
            {
                reduced.Add(msg);
            }

            return reduced;
        }
        private async Task<string> TryFallbackResponseAsync(IChatCompletionService chatService, OpenAIPromptExecutionSettings settings, ChatHistory originalHistory)
        {
            try
            {
                var fallbackHistory = new ChatHistory();
                fallbackHistory.AddSystemMessage(ChatConstants.FallbackSystemPrompt);

                var lastUserMessage = originalHistory.LastOrDefault(m => m.Role == AuthorRole.User);
                if (lastUserMessage != null)
                {
                    fallbackHistory.AddUserMessage(lastUserMessage.Content);
                }
                else
                {
                    fallbackHistory.AddUserMessage(ChatConstants.DefaultFallbackMessage);
                }
                var fallbackSettings = new OpenAIPromptExecutionSettings
                {
                    Temperature = settings.Temperature,
                    TopP = settings.TopP,
                    MaxTokens = Math.Min(settings.MaxTokens ?? 1000, 500), // Reduced for fallback
                    ToolCallBehavior = null // Disable tools for fallback
                };
                var result = await chatService.GetChatMessageContentAsync(fallbackHistory, settings);
                return result?.Content ?? MessageConstant.AI.ResponseGenerationFailed;
            }
            catch
            {
                return MessageConstant.AI.ResponseGenerationFailed;
            }
        }
        private async Task<AIConfiguration> GetAIConfigurationAsync(string modelName)
        {
            return await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.ModelName == modelName && c.IsActive);
        }

        private async Task<AIConfiguration> GetDefaultAIConfigurationAsync()
        {
            var config = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.IsActive);

            return config ?? throw new InvalidOperationException(MessageConstant.Admin.NoActiveConfig);
        }
    }
}
