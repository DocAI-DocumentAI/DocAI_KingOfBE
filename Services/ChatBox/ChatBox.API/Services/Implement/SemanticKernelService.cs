using ChatBox.API.Plugins;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using ChatBox.Infrastructure.Repository.Interfaces;

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
                throw new ArgumentException($"Không tìm thấy cấu hình cho model: {modelName}");

            var builder = Kernel.CreateBuilder();

            // Cấu hình các connector khác nhau
            switch (config.Provider.ToLower())
            {
                case "openai":
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

                case "openrouter":
                    builder.AddOpenAIChatCompletion(
                        modelId: config.ModelName,
                        apiKey: config.ApiKey,
                        endpoint: new Uri(config.Endpoint));
                    break;

                default:
                    throw new NotSupportedException($"Provider {config.Provider} chưa được hỗ trợ");
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
            var executionSettings = CreateExecutionSettings(config);

            var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings);
            return result.Content;
        }

        public async Task<IAsyncEnumerable<string>> GetChatResponseStreamAsync(string modelName, ChatHistory chatHistory)
        {
            var kernel = await GetKernelAsync(modelName);
            var config = await GetAIConfigurationAsync(modelName);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var executionSettings = CreateExecutionSettings(config);
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
            if (chatHistory.Count <= 10)
                return chatHistory;

            // Giữ lại system message và 5 tin nhắn gần nhất
            var reducedHistory = new ChatHistory();

            // Thêm system message nếu có
            var systemMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
            if (systemMessage != null)
            {
                reducedHistory.Add(systemMessage);
            }

            // Thêm 5 tin nhắn gần nhất
            var recentMessages = chatHistory.Skip(Math.Max(0, chatHistory.Count - 5)).ToList();
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

            var titleFunction = kernel.CreateFunctionFromPrompt(@"
Hãy tạo một tiêu đề ngắn gọn (tối đa 50 ký tự) cho cuộc trò chuyện dựa trên tin nhắn đầu tiên của người dùng.
Tiêu đề phải bằng tiếng Việt, súc tích và thể hiện chủ đề chính.

Tin nhắn: {{$input}}

Tiêu đề:");

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
        private OpenAIPromptExecutionSettings CreateExecutionSettings(AIConfiguration config)
        {
            return new OpenAIPromptExecutionSettings
            {
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

            return config ?? throw new InvalidOperationException("Không tìm thấy cấu hình AI nào được kích hoạt");
        }
    }
}
