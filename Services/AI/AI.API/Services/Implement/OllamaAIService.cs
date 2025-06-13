using System.Text;
using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.Domain.Configuration;
using AI.Domain.Models;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace AI.API.Services.Implement
{
    public class OllamaAIService : IOllamaAIService
    {
        private readonly ILogger<OllamaAIService> _logger;
        private readonly OllamaApiClient _ollamaClient;
        private readonly string _ollamaHost;
        private readonly string _modelName;
        private readonly string _embeddingModelName; // REVIEW POINT: Field mới cho model embedding

        private readonly IConfiguration _configuration;

        public OllamaAIService(ILogger<OllamaAIService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _ollamaHost = _configuration.GetValue<string>("Ollama:Host") ?? throw new InvalidOperationException("Ollama:Host configuration is missing.");
            _modelName = _configuration.GetValue<string>("Ollama:ModelName") ?? throw new InvalidOperationException("Ollama:ModelName configuration is missing.");
            // REVIEW POINT: Lấy tên model embedding từ cấu hình
            _embeddingModelName = _configuration.GetValue<string>("Ollama:EmbeddingModelName") ?? throw new InvalidOperationException("Ollama:EmbeddingModelName configuration is missing.");


            _ollamaClient = new OllamaApiClient(_ollamaHost, _modelName); // Sử dụng modelName mặc định cho chat
            _logger.LogInformation($"OllamaAIService initialized. Host: {_ollamaHost}, Chat Model: {_modelName}, Embedding Model: {_embeddingModelName}");

            // Kiểm tra kết nối tới Ollama và sự tồn tại của cả hai mô hình (chat và embedding)
            Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation($"Attempting to connect to Ollama server at {_ollamaHost}...");
                    var isRunning = await _ollamaClient.IsRunningAsync();
                    if (!isRunning)
                    {
                        _logger.LogWarning($"Ollama server is not running or not reachable at {_ollamaHost} at startup. AI functionalities might be impacted.");
                    }
                    else
                    {
                        _logger.LogInformation($"Successfully connected to Ollama server at {_ollamaHost}.");
                        var models = await _ollamaClient.ListLocalModelsAsync();
                        _logger.LogInformation($"Available models on Ollama: {string.Join(", ", models.Select(m => m.Name))}");

                        // REVIEW POINT: Kiểm tra sự tồn tại của cả hai mô hình
                        if (!models.Any(m => m.Name.Equals(_modelName, StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogError($"Chat model '{_modelName}' not found on Ollama server. Please pull it using 'ollama pull {_modelName}'.");
                            throw new InvalidOperationException($"Chat model '{_modelName}' is missing.");
                        }
                        if (!models.Any(m => m.Name.Equals(_embeddingModelName, StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogError($"Embedding model '{_embeddingModelName}' not found on Ollama server. Please pull it using 'ollama pull {_embeddingModelName}'.");
                            throw new InvalidOperationException($"Embedding model '{_embeddingModelName}' is missing.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to connect to Ollama server or verify models at {_ollamaHost} during startup. Please ensure Ollama is running and accessible.");
                    throw;
                }
            }).Wait();
        }
        // Phương thức cho non-streaming response
        public async Task<AIResponse> GenerateResponseAsync(AIRequest request)
        {
            _logger.LogInformation($"Generating response (non-streaming) for model {_modelName} with user question: {request.Question.Substring(0, Math.Min(request.Question.Length, 100))}...");

            var messages = BuildOllamaMessages(request.SystemPrompt, request.Question, request.Documents);
            var options = GetChatRequestOptions(); // Lấy options từ cấu hình

            var chatRequest = new ChatRequest
            {
                Model = _modelName, // Sử dụng _modelName từ field
                Messages = messages,
                Options = options // Truyền đúng kiểu OllamaSharp.Models.Chat.RequestOptions
            };

            var chatResponseBuilder = new StringBuilder();
            try
            {
                // Gọi API ChatAsync và xử lý từng chunk
                await foreach (var chunk in _ollamaClient.ChatAsync(chatRequest))
                {
                    if (chunk?.Message?.Content != null)
                    {
                        chatResponseBuilder.Append(chunk.Message.Content);
                    }
                }

                string finalResponse = chatResponseBuilder.ToString().Trim();
                _logger.LogInformation($"Successfully received complete non-streaming response from Ollama. Generated characters: {finalResponse.Length}.");

                return new AIResponse
                {
                    Answer = finalResponse,
                    ModelUsed = _modelName, // Trả về tên model đã dùng
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during Ollama non-streaming chat generation for model {_modelName}. Request: {request.Question}.");
                throw new ApplicationException("Failed to generate AI response.", ex); // Ném lỗi ứng dụng
            }
        }
        // Phương thức cho streaming response
        // REVIEW POINT: Sửa lỗi 'yield' trong try-catch một cách triệt để
        // Phương thức này chỉ gọi PerformStreamingInference, mà không chứa try-catch trực tiếp
        public async IAsyncEnumerable<string> StreamGenerateResponseAsync(AIRequest request)
        {
            _logger.LogInformation($"Streaming response for model {_modelName} with user question: {request.Question.Substring(0, Math.Min(request.Question.Length, 100))}...");

            var messages = BuildOllamaMessages(request.SystemPrompt, request.Question, request.Documents);
            var options = GetChatRequestOptions(); // Lấy options từ cấu hình

            var chatRequest = new ChatRequest
            {
                Model = _modelName,
                Messages = messages,
                Options = options
            };

            // Gọi local function để xử lý streaming với try-catch riêng.
            // Kết quả yield từ PerformStreamingInference được yield return tiếp bởi phương thức này.
            await foreach (var content in PerformStreamingInference(chatRequest))
            {
                yield return content;
            }

            _logger.LogInformation($"Successfully completed streaming response from Ollama for model {_modelName}.");
        }

        // Local async iterator function riêng biệt để xử lý streaming và try-catch
        // Đây là giải pháp chuẩn để xử lý lỗi "Cannot yield in try-catch"
        private async IAsyncEnumerable<string> PerformStreamingInference(ChatRequest chatRequest)
        {
            //try
            //{
                await foreach (var chunk in _ollamaClient.ChatAsync(chatRequest))
                {
                    if (chunk?.Message?.Content != null)
                    {
                        yield return chunk.Message.Content;
                    }
                }
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, $"Error during Ollama streaming chat inference for model {chatRequest.Model}.");
            //    // Ném lại một ApplicationException để lỗi được bắt ở lớp cao hơn (Controller)
            //    throw new ApplicationException("Failed to stream AI response.", ex);
            //}
        }

        // Xây dựng danh sách tin nhắn cho Ollama
        private List<Message> BuildOllamaMessages(string systemPrompt, string userQuestion, List<Document> documents)
        {
            var messages = new List<Message>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new Message(ChatRole.System, systemPrompt));
            }

            var userMessageContent = new StringBuilder();
            userMessageContent.AppendLine(userQuestion);

            if (documents != null && documents.Any())
            {
                userMessageContent.AppendLine("\n--- Relevant Documents ---");
                foreach (var doc in documents.OrderBy(d => d.DocumentName).ThenBy(d => d.ChunkId))
                {
                    userMessageContent.AppendLine($"Document: {doc.DocumentName ?? "N/A"} (Title: {doc.Title ?? "N/A"})");
                    userMessageContent.AppendLine($"Chunk ID: {doc.ChunkId}");
                    userMessageContent.AppendLine($"Content:\n{doc.Content}");
                    userMessageContent.AppendLine("---");
                }
                userMessageContent.AppendLine("--- End of Relevant Documents ---");
            }

            messages.Add(new Message(ChatRole.User, userMessageContent.ToString()));

            return messages;
        }

        // REVIEW POINT: Lấy các tùy chọn request từ IConfiguration và trả về OllamaSharp.Models.Options
        private RequestOptions GetChatRequestOptions()
        {
            // Sử dụng GetValue<T>() để lấy giá trị từ cấu hình, cung cấp giá trị mặc định nếu không tìm thấy
            var temperature = _configuration.GetValue<float>("Ollama:Temperature", 0.7f);
            var topP = _configuration.GetValue<float>("Ollama:TopP", 0.9f);
            var numPredict = _configuration.GetValue<int>("Ollama:NumPredict", 1024);

            return new RequestOptions // Đây là class OllamaSharp.Models.Chat.RequestOptions
            {
                Temperature = temperature,
                TopP = topP,
                NumPredict = numPredict,
                // Nếu bạn có các tùy chọn khác như Seed, Stop trong appsettings.json, hãy lấy chúng ở đây.
                // Ví dụ:
                // Seed = _configuration.GetValue<int?>("Ollama:Seed"),
                // Stop = _configuration.GetSection("Ollama:Stop").Get<string[]>() // Chú ý: Stop là string[]
            };
        }

        public async Task<EmbeddingResponse> GenerateEmbeddingAsync(EmbeddingRequest request)
        {
            _logger.LogInformation($"Generating embedding for text (length: {request.Text.Length}) using model {_embeddingModelName}.");

            if (string.IsNullOrEmpty(request.Text))
            {
                _logger.LogWarning("Embedding request received with empty text.");
                throw new ArgumentException("Input text for embedding cannot be empty.", nameof(request.Text));
            }

            try
            {
                // Tạo EmbedRequest của OllamaSharp
                var ollamaEmbedRequest = new OllamaSharp.Models.EmbedRequest
                {
                    Model = _embeddingModelName, // Sử dụng mô hình embedding đã cấu hình
                    Input = new List<string> { request.Text }, // Chỉ có một chuỗi văn bản để nhúng
                    // Bạn có thể thêm các Options nếu mô hình embedding hỗ trợ và cần cấu hình
                    // Options = new OllamaSharp.Models.Chat.RequestOptions { ... }
                };

                // Gọi API EmbedAsync của OllamaClient
                var ollamaEmbedResponse = await _ollamaClient.EmbedAsync(ollamaEmbedRequest);

                // OllamaSharp trả về List<float[]>. Đối với một input string, chúng ta lấy [0].
                if (ollamaEmbedResponse?.Embeddings != null && ollamaEmbedResponse.Embeddings.Any())
                {
                    var embeddingVector = ollamaEmbedResponse.Embeddings[0].ToList(); // Chuyển float[] sang List<float>
                    _logger.LogInformation($"Successfully generated embedding for text. Vector length: {embeddingVector.Count}.");

                    return new EmbeddingResponse
                    {
                        Embedding = embeddingVector,
                        ModelUsed = _embeddingModelName
                    };
                }
                else
                {
                    _logger.LogError($"Ollama returned empty embeddings for text. Model: {_embeddingModelName}");
                    throw new InvalidOperationException("Ollama did not return a valid embedding.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating embedding with model {_embeddingModelName} for text: {request.Text.Substring(0, Math.Min(request.Text.Length, 100))}.");
                throw new ApplicationException("Failed to generate embedding.", ex);
            }
        }
    }

}