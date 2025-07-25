using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextGeneration;

namespace AI.API.Services
{
    public class HuggingFaceTextService : ITextGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _endpoint;
        private readonly ILogger<HuggingFaceTextService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public HuggingFaceTextService(
             HttpClient httpClient,
             string apiKey,
             string model,
             string endpoint,
             ILogger<HuggingFaceTextService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = !string.IsNullOrEmpty(apiKey) ? apiKey : throw new ArgumentNullException(nameof(apiKey));
            _model = !string.IsNullOrEmpty(model) ? model : throw new ArgumentNullException(nameof(model));
            _endpoint = !string.IsNullOrEmpty(endpoint) ? endpoint : throw new ArgumentNullException(nameof(endpoint));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure HttpClient with optimized settings
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DocAI-Service/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // Long timeout for text generation

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                PropertyNameCaseInsensitive = true
            };
        }

        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>
        {
            ["model"] = _model,
            ["endpoint"] = _endpoint,
            ["provider"] = "HuggingFace",
            ["service_type"] = "text_generation"
        };

        public async Task<IReadOnlyList<TextContent>> GetTextContentsAsync(
            string prompt,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                _logger.LogDebug("[{RequestId}] Starting text generation. Model: {Model}, Prompt length: {Length}",
                    requestId, _model, prompt?.Length ?? 0);

                // Validate input
                if (string.IsNullOrEmpty(prompt))
                {
                    throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));
                }

                var requestPayload = CreateChatCompletionPayload(prompt, executionSettings);
                _logger.LogTrace("[{RequestId}] Request payload: {Payload}", requestId, requestPayload);

                var (response, tokensUsed) = await SendRequestAsync(requestPayload, requestId, cancellationToken);
                var content = ExtractContentFromResponse(response, requestId);

                _logger.LogInformation("[{RequestId}] Text generation completed successfully. " +
                    "Content length: {Length}, Tokens used: {Tokens}",
                    requestId, content.Length, tokensUsed);

                var textContent = new TextContent(content);

                var metadata = new Dictionary<string, object?>
                {
                    ["model"] = _model,
                    ["request_id"] = requestId
                };

                if (tokensUsed > 0)
                {
                    metadata["tokens_used"] = tokensUsed;
                }
                textContent.Metadata = metadata;


                return new List<TextContent> { textContent };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[{RequestId}] Text generation was cancelled", requestId);
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[{RequestId}] HTTP error in text generation", requestId);
                return new List<TextContent>
                {
                    new TextContent($"Network error: {ex.Message}")
                    {
                        Metadata = new Dictionary<string, object?>
                        {
                            ["error"] = true,
                            ["error_type"] = "network_error",
                            ["request_id"] = requestId
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Unexpected error in text generation", requestId);
                return new List<TextContent>
                {
                    new TextContent($"AI service error: {ex.Message}")
                    {
                        Metadata = new Dictionary<string, object?>
                        {
                            ["error"] = true,
                            ["error_type"] = ex.GetType().Name,
                            ["request_id"] = requestId
                        }
                    }
                };
            }
        }

        public async IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(
            string prompt,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var chunks = new List<StreamingTextContent>();
            var totalTokens = 0;

            try
            {
                _logger.LogDebug("[{RequestId}] Starting streaming text generation. Model: {Model}", requestId, _model);

                // Validate input
                if (string.IsNullOrEmpty(prompt))
                {
                    chunks.Add(new StreamingTextContent("Error: Prompt cannot be empty")
                    {
                        Metadata = new Dictionary<string, object?>
                        {
                            ["error"] = true,
                            ["request_id"] = requestId
                        }
                    });
                }
                else
                {
                    var requestPayload = CreateChatCompletionPayload(prompt, executionSettings, stream: true);

                    using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                    {
                        Content = new StringContent(requestPayload, Encoding.UTF8, "application/json")
                    };

                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    _logger.LogDebug("[{RequestId}] Streaming response status: {StatusCode}", requestId, response.StatusCode);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        var errorMessage = ExtractErrorMessage(errorContent, response.StatusCode);

                        _logger.LogError("[{RequestId}] Streaming API error {StatusCode}: {Error}",
                            requestId, response.StatusCode, errorMessage);

                        chunks.Add(new StreamingTextContent($"Error {response.StatusCode}: {errorMessage}")
                        {
                            Metadata = new Dictionary<string, object?>
                            {
                                ["error"] = true,
                                ["status_code"] = (int)response.StatusCode,
                                ["request_id"] = requestId
                            }
                        });
                    }
                    else
                    {
                        await foreach (var chunk in ProcessStreamResponseAsync(response, requestId, cancellationToken))
                        {
                            if (!string.IsNullOrEmpty(chunk.Text))
                            {
                                totalTokens += EstimateTokens(chunk.Text);

                                // Tạo bản sao metadata để chỉnh sửa
                                var updatedMetadata = new Dictionary<string, object?>(chunk.Metadata)
                                {
                                    ["tokens_estimate"] = EstimateTokens(chunk.Text),
                                    ["request_id"] = requestId
                                };

                                // Tạo chunk mới với metadata cập nhật
                                var updatedChunk = new StreamingTextContent(chunk.Text)
                                {
                                    Metadata = updatedMetadata
                                };

                                chunks.Add(updatedChunk);
                            }
                            else
                            {
                                chunks.Add(chunk);
                            }
                        }

                        _logger.LogInformation("[{RequestId}] Streaming completed. Total estimated tokens: {Tokens}",
                            requestId, totalTokens);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[{RequestId}] Streaming was cancelled", requestId);
                chunks.Add(new StreamingTextContent("")
                {
                    Metadata = new Dictionary<string, object?>
                    {
                        ["cancelled"] = true,
                        ["request_id"] = requestId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error in streaming generation", requestId);
                chunks.Add(new StreamingTextContent($"Stream error: {ex.Message}")
                {
                    Metadata = new Dictionary<string, object?>
                    {
                        ["error"] = true,
                        ["error_type"] = ex.GetType().Name,
                        ["request_id"] = requestId
                    }
                });
            }

            // Yield chunks outside try-catch (C# requirement for yield)
            foreach (var chunk in chunks)
            {
                yield return chunk;
            }

            // Send final completion chunk if not already sent
            if (chunks.Count > 0 && !chunks.Any(c => c.Metadata.ContainsKey("is_complete")))
            {
                yield return new StreamingTextContent("")
                {
                    Metadata = new Dictionary<string, object?>
                    {
                        ["is_complete"] = true,
                        ["total_tokens_estimate"] = totalTokens,
                        ["request_id"] = requestId
                    }
                };
            }
        }
        #region Private Methods

        private string CreateChatCompletionPayload(string prompt, PromptExecutionSettings? settings, bool stream = false)
        {
            // Build messages array - support system + user messages
            var messages = new List<object>();

            // Check if settings contain system prompt
            if (settings?.ExtensionData?.TryGetValue("system_prompt", out var systemPromptObj) == true)
            {
                var systemPrompt = systemPromptObj?.ToString();
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    messages.Add(new { role = "system", content = systemPrompt });
                }
            }

            // Add user message
            messages.Add(new { role = "user", content = prompt });

            var payload = new
            {
                model = _model,
                messages,
                stream,
                max_tokens = GetSettingValue(settings, "max_tokens", 2048),
                temperature = Math.Max(0.0, Math.Min(2.0, GetSettingValue(settings, "temperature", 0.7))),
                top_p = Math.Max(0.0, Math.Min(1.0, GetSettingValue(settings, "top_p", 0.9))),
                stop = GetSettingValue<string[]>(settings, "stop", null),
                presence_penalty = Math.Max(-2.0, Math.Min(2.0, GetSettingValue(settings, "presence_penalty", 0.0))),
                frequency_penalty = Math.Max(-2.0, Math.Min(2.0, GetSettingValue(settings, "frequency_penalty", 0.0)))
            };

            return JsonSerializer.Serialize(payload, _jsonOptions);
        }

        private async Task<(string response, int tokensUsed)> SendRequestAsync(string payload, string requestId, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            const int baseDelayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };

                    _logger.LogTrace("[{RequestId}] Sending request (attempt {Attempt}/{MaxRetries}) to {Endpoint}",
                        requestId, attempt, maxRetries, _endpoint);

                    using var response = await _httpClient.SendAsync(request, cancellationToken);
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var tokensUsed = ExtractTokenUsage(responseContent);
                        _logger.LogDebug("[{RequestId}] Request successful on attempt {Attempt}. Tokens: {Tokens}",
                            requestId, attempt, tokensUsed);
                        return (responseContent, tokensUsed);
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < maxRetries)
                    {
                        var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                        _logger.LogWarning("[{RequestId}] Rate limited (attempt {Attempt}). Retrying in {Delay}ms",
                            requestId, attempt, delay);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    var errorMessage = ExtractErrorMessage(responseContent, response.StatusCode);
                    _logger.LogError("[{RequestId}] API error on attempt {Attempt}: {StatusCode} - {Error}",
                        requestId, attempt, response.StatusCode, errorMessage);

                    throw new HttpRequestException($"HuggingFace API error {response.StatusCode}: {errorMessage}");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("[{RequestId}] Request cancelled on attempt {Attempt}", requestId, attempt);
                    throw;
                }
                catch (HttpRequestException)
                {
                    throw; // Re-throw API errors
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                    _logger.LogWarning(ex, "[{RequestId}] Network error on attempt {Attempt}. Retrying in {Delay}ms",
                        requestId, attempt, delay);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            throw new HttpRequestException($"Failed to get response after {maxRetries} attempts");
        }

        private async IAsyncEnumerable<StreamingTextContent> ProcessStreamResponseAsync(
            HttpResponseMessage response,
            string requestId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var streamChunks = new List<StreamingTextContent>();
            var lineCount = 0;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
            {
                lineCount++;
                _logger.LogTrace("[{RequestId}] Stream line {LineCount}: {Line}", requestId, lineCount, line);

                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                    continue;

                var data = line.Substring(6).Trim();
                if (data == "[DONE]")
                {
                    _logger.LogDebug("[{RequestId}] Stream completed with [DONE] after {LineCount} lines", requestId, lineCount);

                    streamChunks.Add(new StreamingTextContent("")
                    {
                        Metadata = new Dictionary<string, object?>
                        {
                            ["is_complete"] = true,
                            ["total_lines"] = lineCount
                        }
                    });
                    break;
                }

                try
                {
                    using var jsonDoc = JsonDocument.Parse(data);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                    {
                        var firstChoice = choices.EnumerateArray().FirstOrDefault();
                        if (firstChoice.ValueKind != JsonValueKind.Undefined &&
                            firstChoice.TryGetProperty("delta", out var delta) &&
                            delta.TryGetProperty("content", out var content))
                        {
                            var contentText = content.GetString();
                            if (!string.IsNullOrEmpty(contentText))
                            {
                                streamChunks.Add(new StreamingTextContent(contentText)
                                {
                                    Metadata = new Dictionary<string, object?>
                                    {
                                        ["line_number"] = lineCount,
                                        ["chunk_length"] = contentText.Length
                                    }
                                });
                            }
                        }

                        // Check for finish reason
                        if (firstChoice.TryGetProperty("finish_reason", out var finishReason) &&
                            finishReason.ValueKind != JsonValueKind.Null)
                        {
                            var reason = finishReason.GetString();
                            _logger.LogDebug("[{RequestId}] Stream finished with reason: {Reason}", requestId, reason);

                            streamChunks.Add(new StreamingTextContent("")
                            {
                                Metadata = new Dictionary<string, object?>
                                {
                                    ["is_complete"] = true,
                                    ["finish_reason"] = reason
                                }
                            });
                            break;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "[{RequestId}] Failed to parse streaming chunk on line {LineCount}: {Data}",
                        requestId, lineCount, data);

                    streamChunks.Add(new StreamingTextContent("")
                    {
                        Metadata = new Dictionary<string, object?>
                        {
                            ["parse_error"] = true,
                            ["error_line"] = lineCount
                        }
                    });
                }
            }

            _logger.LogDebug("[{RequestId}] Stream processing completed. Total chunks: {ChunkCount}",
                requestId, streamChunks.Count);

            foreach (var chunk in streamChunks)
            {
                yield return chunk;
            }
        }

        private string ExtractContentFromResponse(string response, string requestId)
        {
            try
            {
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                _logger.LogTrace("[{RequestId}] Parsing response JSON structure", requestId);

                // Standard OpenAI-compatible format
                if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                {
                    var firstChoice = choices.EnumerateArray().FirstOrDefault();
                    if (firstChoice.ValueKind != JsonValueKind.Undefined &&
                        firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        var contentText = content.GetString();
                        if (!string.IsNullOrEmpty(contentText))
                        {
                            _logger.LogDebug("[{RequestId}] Successfully extracted content from message.content", requestId);
                            return contentText.Trim();
                        }
                    }

                    // Fallback to text property
                    if (firstChoice.TryGetProperty("text", out var text))
                    {
                        var textContent = text.GetString();
                        if (!string.IsNullOrEmpty(textContent))
                        {
                            _logger.LogDebug("[{RequestId}] Extracted content from text property", requestId);
                            return textContent.Trim();
                        }
                    }
                }

                // Direct response format
                if (root.TryGetProperty("text", out var directText))
                {
                    var textContent = directText.GetString();
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        _logger.LogDebug("[{RequestId}] Extracted content from direct text", requestId);
                        return textContent.Trim();
                    }
                }

                _logger.LogWarning("[{RequestId}] No content found in response structure", requestId);
                return "No content found in response";
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[{RequestId}] Failed to parse response JSON", requestId);
                return $"Failed to parse response: {ex.Message}";
            }
        }

        private int ExtractTokenUsage(string response)
        {
            try
            {
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                if (root.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("total_tokens", out var totalTokens))
                    {
                        return totalTokens.GetInt32();
                    }
                    if (usage.TryGetProperty("completion_tokens", out var completionTokens))
                    {
                        return completionTokens.GetInt32();
                    }
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private string ExtractErrorMessage(string responseContent, HttpStatusCode statusCode)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                // Try different error message paths
                if (root.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("message", out var message))
                    {
                        return message.GetString() ?? statusCode.ToString();
                    }
                    if (error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetString() ?? statusCode.ToString();
                    }
                }

                if (root.TryGetProperty("detail", out var detail))
                {
                    return detail.GetString() ?? statusCode.ToString();
                }

                return statusCode.ToString();
            }
            catch
            {
                return responseContent.Length > 200 ? responseContent.Substring(0, 200) + "..." : responseContent;
            }
        }

        private int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            // Improved token estimation for various languages
            var charCount = text.Length;
            var wordCount = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

            // For Vietnamese and mixed content, use a conservative estimate
            return Math.Max(1, (int)Math.Ceiling(charCount / 3.5 + wordCount / 0.75));
        }

        private T GetSettingValue<T>(PromptExecutionSettings? settings, string key, T defaultValue)
        {
            if (settings?.ExtensionData?.TryGetValue(key, out var value) == true)
            {
                try
                {
                    if (value is T directValue) return directValue;
                    if (value is JsonElement jsonElement)
                    {
                        return JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), _jsonOptions) ?? defaultValue;
                    }
                    return (T)Convert.ChangeType(value, typeof(T)) ?? defaultValue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert setting {Key} to type {Type}, using default", key, typeof(T));
                }
            }
            return defaultValue;
        }

        #endregion
    }

}