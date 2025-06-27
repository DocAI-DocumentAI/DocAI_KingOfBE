using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using OllamaSharp;
using OllamaSharp.Models;

namespace AI.API.Services.Implement
{
    public class OllamaTextEmbeddingGeneratorKm : ITextEmbeddingGenerator, ITextEmbeddingBatchGenerator
    {
        private readonly ILogger<OllamaTextEmbeddingGeneratorKm> _log;
        private readonly OllamaApiClient _ollamaClient;
        private readonly string _embeddingModelName;
        private readonly IConfiguration _configuration;
        private readonly ITextTokenizer _textTokenizer;

        public int MaxTokens { get; }
        public int MaxBatchSize { get; } = 1;

        public OllamaTextEmbeddingGeneratorKm(
           ILogger<OllamaTextEmbeddingGeneratorKm> logger,
           IConfiguration configuration,
           ITextTokenizer? textTokenizer = null) // Inject ITextTokenizer
        {
            _log = logger;
            _configuration = configuration;

            var ollamaHost = _configuration.GetValue<string>("Ollama:Host") ?? throw new InvalidOperationException("Ollama:Host configuration is missing.");
            _embeddingModelName = _configuration.GetValue<string>("Ollama:EmbeddingModel") ?? throw new InvalidOperationException("Ollama:EmbeddingModel configuration is missing.");

            _ollamaClient = new OllamaApiClient(ollamaHost, _embeddingModelName);

            _textTokenizer = textTokenizer ?? new Microsoft.KernelMemory.AI.CL100KTokenizer(); // REVIEW POINT: Sử dụng CL100KTokenizer
            if (_textTokenizer == null)
            {
                _log.LogWarning("Text tokenizer not specified for OllamaTextEmbeddingGeneratorKm, token counts might be inaccurate.");
            }

            MaxTokens = _configuration.GetValue<int>("Ollama:EmbeddingMaxTokenTotal", 2048);
            if (MaxTokens <= 0) MaxTokens = 2048;

            _log.LogInformation("OllamaTextEmbeddingGeneratorKm initialized. Host: {Host}, Model: {Model}, MaxTokens: {MaxTokens}", ollamaHost, _embeddingModelName, MaxTokens);

            Task.Run(async () =>
            {
                try
                {
                    _log.LogInformation("Attempting to connect to Ollama server for embedding generation...");
                    if (!await _ollamaClient.IsRunningAsync())
                    {
                        _log.LogWarning("Ollama server not reachable for embedding generation.");
                    }
                    else
                    {
                        var models = await _ollamaClient.ListLocalModelsAsync();
                        if (!models.Any(m => m.Name.Equals(_embeddingModelName, StringComparison.OrdinalIgnoreCase)))
                        {
                            _log.LogError($"Embedding model '{_embeddingModelName}' not found on Ollama server. Please pull it using 'ollama pull {_embeddingModelName}'.");
                            throw new InvalidOperationException($"Embedding model '{_embeddingModelName}' is missing.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to initialize OllamaTextEmbeddingGeneratorKm.");
                    throw;
                }
            }).Wait();
        }


        public async Task<Embedding> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            _log.LogTrace("Generating embedding for text (length: {TextLength} chars) with model {ModelName}", text.Length, _embeddingModelName);

            var ollamaEmbedRequest = new OllamaSharp.Models.EmbedRequest
            {
                Model = _embeddingModelName,
                Input = new List<string> { text }, // Chỉ có một chuỗi văn bản để nhúng
                Options = null // KM ITextEmbeddingGenerator không truyền options cụ thể
            };

            EmbedResponse response = await _ollamaClient.EmbedAsync(ollamaEmbedRequest, cancellationToken);

            if (response?.Embeddings != null && response.Embeddings.Any())
            {
                // OllamaSharp trả về List<float[]>. Lấy vector đầu tiên.
                return new Embedding(response.Embeddings[0]);
            }
            _log.LogError("Ollama returned empty embeddings for text with model {ModelName}", _embeddingModelName);
            throw new KernelMemoryException($"Failed to generate embedding: empty response from Ollama.");
        }

        // Implement GenerateEmbeddingBatchAsync cho ITextEmbeddingBatchGenerator
        // Ollama hiện tại (thông qua OllamaSharp) không hỗ trợ batch embedding API trực tiếp.
        // Chúng ta sẽ giả lập batching bằng cách gọi từng cái một.
        public async Task<Embedding[]> GenerateEmbeddingBatchAsync(IEnumerable<string> textList, CancellationToken cancellationToken = default)
        {
            var embeddings = new List<Embedding>();
            foreach (var text in textList)
            {
                // Call GenerateEmbeddingAsync cho từng text trong batch
                embeddings.Add(await GenerateEmbeddingAsync(text, cancellationToken));
            }
            return embeddings.ToArray();
        }

        public int CountTokens(string text)
        {
            return _textTokenizer.CountTokens(text);
        }

        public IReadOnlyList<string> GetTokens(string text)
        {
            return _textTokenizer.GetTokens(text);
        }
    }
}
