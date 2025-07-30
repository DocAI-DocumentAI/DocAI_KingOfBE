using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using Microsoft.AspNetCore.Connections;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ChatBox.API.Services.Implement
{
    public class DocumentSearchService : IDocumentSearchService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DocumentSearchService> _logger;
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new();

        public DocumentSearchService(IConfiguration configuration, ILogger<DocumentSearchService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            InitializeRabbitMQ();
        }

        private async Task InitializeRabbitMQ()
        {
            try
            {
                var factory = new ConnectionFactory()
                {
                    Uri = new Uri(_configuration["RabbitMQ:ConnectionString"]),
                    UserName = _configuration["RabbitMQ:Username"],
                    Password = _configuration["RabbitMQ:Password"],
                    AutomaticRecoveryEnabled = true
                };

                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();

                // ✅ Declare queues
                var searchQueue = _configuration["RabbitMQ:DocumentService:SearchQueue"];
                var responseQueue = _configuration["RabbitMQ:DocumentService:SearchResponseQueue"];

                await _channel.QueueDeclareAsync(queue: searchQueue, durable: true, exclusive: false, autoDelete: false);
                await _channel.QueueDeclareAsync(queue: responseQueue, durable: true, exclusive: false, autoDelete: false);

                // ✅ Setup response consumer
                SetupResponseConsumer(responseQueue);

                _logger.LogInformation("RabbitMQ initialized for DocumentSearchService");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ");
                throw;
            }
        }

        private async void SetupResponseConsumer(string responseQueue)
        {
            if (_channel == null) return;

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var correlationId = ea.BasicProperties.CorrelationId;
                    var body = ea.Body.ToArray();
                    var responseJson = Encoding.UTF8.GetString(body);

                    if (_pendingRequests.TryRemove(correlationId, out var tcs))
                    {
                        tcs.SetResult(responseJson);
                    }

                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing document search response");
                    await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
                await Task.CompletedTask;
            };

          await  _channel.BasicConsumeAsync(queue: responseQueue, autoAck: false, consumer: consumer);
        }

        // ✅ MAIN METHOD - Search documents with RAG
        public async Task<DocumentResponse?> SearchDocumentsWithRAGAsync(string query, string userId, int maxResults = 5)
        {
            if (_channel == null)
            {
                _logger.LogError("RabbitMQ channel not initialized");
                return null;
            }

            var requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<string>();
            _pendingRequests[requestId] = tcs;

            try
            {
                var request = new DocumentRequest
                {
                    RequestId = requestId,
                    Query = query,
                    UserId = userId,
                    MaxResults = maxResults,
                    MinRelevanceScore = 0.7,
                    OnlyPublic = true,
                    RequestTime = DateTime.UtcNow
                };

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));

                var properties = new BasicProperties
                {
                    ReplyTo = _configuration["RabbitMQ:DocumentService:SearchResponseQueue"],
                    CorrelationId = requestId,
                    Persistent = true
                };

                await _channel.BasicPublishAsync(
                          exchange: "",
                          routingKey: _configuration["RabbitMQ:DocumentService:SearchQueue"],
                          mandatory: false,
                          basicProperties: properties,
                          body: body);

                _logger.LogInformation("Sent document search request: {RequestId} for query: {Query}", requestId, query);

                // ✅ Wait for response
                var timeoutSeconds = _configuration.GetValue<int>("RabbitMQ:DocumentService:RequestTimeoutSeconds", 30);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cts.Token);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _pendingRequests.TryRemove(requestId, out _);
                    _logger.LogWarning("Document search request {RequestId} timed out", requestId);
                    return null;
                }

                cts.Cancel();
                var responseJson = await tcs.Task;
                var response = JsonSerializer.Deserialize<DocumentResponse>(responseJson);

                if (response?.Success == true)
                {
                    _logger.LogInformation("Received document search response: {RequestId}, found {SourceCount} sources",
                        requestId, response.Sources.Count);
                }
                else
                {
                    _logger.LogWarning("Document search failed: {RequestId}, error: {Error}",
                        requestId, response?.ErrorMessage);
                }

                return response;
            }
            catch (Exception ex)
            {
                _pendingRequests.TryRemove(requestId, out _);
                _logger.LogError(ex, "Error sending document search request");
                return null;
            }
        }

        // ✅ CONVENIENCE METHODS for different use cases

        /// <summary>
        /// Get RAG answer for user query
        /// </summary>
        public async Task<string> GetRAGAnswerAsync(string query, string userId)
        {
            var result = await SearchDocumentsWithRAGAsync(query, userId);

            if (result?.Success == true && !string.IsNullOrEmpty(result.Answer))
            {
                return result.Answer;
            }

            return "Xin lỗi, tôi không tìm thấy thông tin phù hợp trong tài liệu để trả lời câu hỏi này.";
        }

        /// <summary>
        /// Get RAG answer with sources for transparency
        /// </summary>
        public async Task<string> GetRAGAnswerWithSourcesAsync(string query, string userId)
        {
            var result = await SearchDocumentsWithRAGAsync(query, userId);

            if (result?.Success == true && !string.IsNullOrEmpty(result.Answer))
            {
                var answer = new StringBuilder();
                answer.AppendLine(result.Answer);

                if (result.Sources.Any())
                {
                    answer.AppendLine();
                    answer.AppendLine("📚 **Nguồn tham khảo:**");

                    foreach (var source in result.Sources.Take(3)) // Show top 3 sources
                    {
                        answer.AppendLine($"• {source.Title} v{source.VersionName} (Score: {source.RelevanceScore:F2})");
                    }
                }

                return answer.ToString();
            }

            return "Xin lỗi, tôi không tìm thấy thông tin phù hợp trong tài liệu để trả lời câu hỏi này.";
        }

        public async Task<DocumentResponse?> SearchOfficialDocumentsAsync(string query, string userId)
        {
            if (_channel == null) return null;

            var requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<string>();
            _pendingRequests[requestId] = tcs;

            try
            {
                var request = new DocumentRequest
                {
                    RequestId = requestId,
                    Query = query,
                    UserId = userId,
                    MaxResults = 5,
                    MinRelevanceScore = 0.8, 
                    OnlyPublic = true,
                    OnlyOfficial = true 
                };

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
                var properties = new BasicProperties
                {
                    ReplyTo = _configuration["RabbitMQ:DocumentService:SearchResponseQueue"],
                    CorrelationId = requestId,
                    Persistent = true
                };

                await _channel.BasicPublishAsync(
                          exchange: "",
                          routingKey: _configuration["RabbitMQ:DocumentService:SearchQueue"],
                          mandatory: false,
                          basicProperties: properties,
                          body: body);

                var timeoutSeconds = _configuration.GetValue<int>("RabbitMQ:DocumentService:RequestTimeoutSeconds", 30);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cts.Token);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _pendingRequests.TryRemove(requestId, out _);
                    return null;
                }

                cts.Cancel();
                var responseJson = await tcs.Task;
                return JsonSerializer.Deserialize<DocumentResponse>(responseJson);
            }
            catch (Exception ex)
            {
                _pendingRequests.TryRemove(requestId, out _);
                _logger.LogError(ex, "Error searching official documents");
                return null;
            }
        }

        public void Dispose()
        {
            try
            {
                foreach (var kvp in _pendingRequests)
                {
                    kvp.Value.TrySetCanceled();
                }
                _pendingRequests.Clear();

                _channel?.CloseAsync(200, "Goodbye").GetAwaiter().GetResult();
                _channel?.Dispose();
                _connection?.CloseAsync().GetAwaiter().GetResult();
                _connection?.Dispose();

                _logger.LogInformation("DocumentSearchService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing DocumentSearchService");
            }
        }
    }
}
