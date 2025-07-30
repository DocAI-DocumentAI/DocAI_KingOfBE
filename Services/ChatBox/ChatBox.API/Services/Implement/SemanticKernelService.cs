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
            var config = await GetAIConfigurationAsync(modelName);
            if (config == null)
                throw new ArgumentException(_configuration["ChatService:Messages:ConfigNotFound"]);

            var builder = Kernel.CreateBuilder();

            // Cấu hình các connector khác nhau
            switch (config.Provider.ToLower())
            {
                case Providers.OPENAI:
                    builder.AddOpenAIChatCompletion(
                        modelId: config.ModelName,
                        apiKey: config.ApiKey);

                    // Thêm embedding cho OpenAI
                    var embeddingModel = _configuration["OpenAI:EmbeddingModel"];
                    if (!string.IsNullOrEmpty(embeddingModel))
                    {
                        builder.AddOpenAITextEmbeddingGeneration(
                            modelId: embeddingModel,
                          apiKey: config.ApiKey);
                   }
                   break;

                case Providers.OPENROUTER:
                    builder.AddOpenAIChatCompletion(
                        modelId: config.ModelName,
                        apiKey: config.ApiKey,
                        endpoint: new Uri(config.Endpoint));
                    break;

                default:
                    throw new NotSupportedException(string.Format(_configuration["ChatService:Messages:ModelNotSupported"], config.Provider));
            }

            // Thêm memory storage
            //builder.Services.(_ => new VolatileMemoryStore());
            // Đăng ký các plugin
            var kernel = builder.Build();
            await RegisterPluginsAsync(kernel);

            return kernel;
        }

        private async Task RegisterPluginsAsync(Kernel kernel)
        {
            // Plugin tìm kiếm tài liệu
            var documentPlugin = new DocumentSearchPlugin(_documentSearchService);
            kernel.Plugins.AddFromObject(documentPlugin, "DocumentSearch");

            // Plugin thời gian
            kernel.Plugins.AddFromType<TimePlugin>("Time");
            //kernel.Plugins.AddFromType<SummaryPlugin>("Summary");

        }

        public async Task<string> GetChatResponseAsync(string modelName, ChatHistory chatHistory)
        {
            var kernel = await GetKernelAsync(modelName);
            var config = await GetAIConfigurationAsync(modelName);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            bool hasSystemMessage = chatHistory.Any(m => m.Role == AuthorRole.System);

            // ✅ Conditional execution settings
            var executionSettings = CreateExecutionSettings(config, hasSystemMessage);
            var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings);
            return result.Content;
        }

        public async Task<IAsyncEnumerable<string>> GetChatResponseStreamAsync(string modelName, ChatHistory chatHistory)
        {
            var kernel = await GetKernelAsync(modelName);
            var config = await GetAIConfigurationAsync(modelName);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            bool hasSystemMessage = chatHistory.Any(m => m.Role == AuthorRole.System);

            // ✅ Conditional execution settings
            var executionSettings = CreateExecutionSettings(config, hasSystemMessage);
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
            var maxHistoryCount = _configuration.GetValue<int>("ChatService:MaxChatHistoryCount");
            if (chatHistory.Count <= maxHistoryCount)
                return chatHistory;

            var reducedHistory = new ChatHistory();

            var systemMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
            if (systemMessage != null)
            {
                reducedHistory.Add(systemMessage);
            }

            var recentMessagesCount = _configuration.GetValue<int>("ChatService:RecentMessagesCount");
            var recentMessages = chatHistory.Skip(Math.Max(0, chatHistory.Count - recentMessagesCount)).ToList();

            foreach (var message in recentMessages)
            {
                if (message.Role != AuthorRole.System)
                {
                    reducedHistory.Add(message);
                }
            }

            return reducedHistory;
        }
//        public async Task<ChatHistory> ReduceChatHistoryAsync(ChatHistory chatHistory)
//        {
//            if (chatHistory.Count <= 10)
//                return chatHistory;

//            var config = await GetDefaultAIConfigurationAsync();
//            var kernel = await GetKernelAsync(config.ModelName);

//            // Use SK to summarize conversation
//            var summaryFunction = kernel.CreateFunctionFromPrompt(@"
//Hãy tóm tắt cuộc hội thoại sau, giữ lại thông tin quan trọng và ngữ cảnh chính:

//{{$conversation}}

//Tóm tắt ngắn gọn bằng tiếng Việt:");

//            var conversationText = string.Join("\n",
//                chatHistory.Skip(1).Take(chatHistory.Count - 6)
//                .Select(m => $"{(m.Role == AuthorRole.User ? "User" : "Assistant")}: {m.Content}"));

//            var summary = await summaryFunction.InvokeAsync(kernel, new KernelArguments
//            {
//                ["conversation"] = conversationText
//            });

//            // Build reduced history
//            var reducedHistory = new ChatHistory();

//            // Keep system message
//            var systemMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
//            if (systemMessage != null)
//            {
//                reducedHistory.Add(systemMessage);
//            }

//            // Add summary
//            reducedHistory.AddAssistantMessage($"Tóm tắt cuộc trò chuyện trước: {summary}");

//            // Keep recent messages
//            var recentMessages = chatHistory.TakeLast(5);
//            foreach (var message in recentMessages)
//            {
//                if (message.Role != AuthorRole.System)
//                {
//                    reducedHistory.Add(message);
//                }
//            }

//            return reducedHistory;
//        }

        public async Task<string> GenerateTitleAsync(string message)
        {
            var config = await GetDefaultAIConfigurationAsync();
            var kernel = await GetKernelAsync(config.ModelName);

            var promptTemplate = _configuration["ChatService:TitleGenerationPrompt"];
            var titleFunction = kernel.CreateFunctionFromPrompt(promptTemplate);

            var result = await titleFunction.InvokeAsync(kernel, new KernelArguments { ["input"] = message });
            return result.ToString().Trim().Replace("\"", "");
        }

        //private OpenAIPromptExecutionSettings CreateExecutionSettings(AIConfiguration config)
        //{
        //    var settings = new OpenAIPromptExecutionSettings
        //    {
        //        Temperature = config.Temperature,
        //        TopP = config.TopP,
        //        MaxTokens = config.MaxTokens
        //    };

        //    // TopK chỉ áp dụng cho một số model (không phải OpenAI)
        //    if (config.TopK.HasValue && config.Provider.ToLower() != "openai")
        //    {
        //        // Custom implementation for providers that support TopK
        //    }

        //    return settings;
        //}
        private OpenAIPromptExecutionSettings CreateExecutionSettings(AIConfiguration config, bool hasSystemMessageInHistory)
        {
            var systemPrompt = _configuration["ChatService:SystemPrompt"];

            return new OpenAIPromptExecutionSettings
            {
                ChatSystemPrompt = hasSystemMessageInHistory ? null : systemPrompt,
                Temperature = config.Temperature,
                TopP = config.TopP,
                MaxTokens = config.MaxTokens,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };
        }
        private async Task<AIConfiguration> GetAIConfigurationAsync(string modelName)
        {
            var config = await _unitOfWork.GetRepository<AIConfiguration>()
                .SingleOrDefaultAsync(predicate: c => c.ModelName == modelName && c.IsActive);

            return config;
        }

        private async Task<AIConfiguration> GetDefaultAIConfigurationAsync()
        {
            var config = await _unitOfWork.GetRepository<AIConfiguration>()
          .SingleOrDefaultAsync(predicate: c => c.IsActive);

            return config ?? throw new InvalidOperationException(_configuration["ChatService:Messages:NoActiveConfig"]);
        }
    }
}
