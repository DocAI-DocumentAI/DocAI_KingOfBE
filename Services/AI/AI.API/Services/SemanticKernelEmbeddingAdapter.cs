using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Embeddings;

namespace AI.API.Services
{
    public class SemanticKernelEmbeddingAdapter : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly ITextEmbeddingGenerationService _embeddingService;
        private readonly ILogger<SemanticKernelEmbeddingAdapter> _logger;

        // Constants for OpenAI limits
        private const int MAX_TEXT_LENGTH = 8191;
        private const int MAX_BATCH_SIZE = 2048;
        private const int MAX_CONCURRENCY = 3;

        public SemanticKernelEmbeddingAdapter(
            ITextEmbeddingGenerationService embeddingService,
            ILogger<SemanticKernelEmbeddingAdapter> logger)
        {
            _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("SemanticKernelEmbeddingAdapter initialized with service: {ServiceType}",
                _embeddingService.GetType().Name);
        }

        public async Task<Embedding<float>> GenerateAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                _logger.LogDebug("[{RequestId}] Generating OpenAI embedding via Semantic Kernel. Text length: {Length}",
                    requestId, text?.Length ?? 0);

                if (string.IsNullOrEmpty(text))
                {
                    throw new ArgumentException("Text cannot be null or empty", nameof(text));
                }

                // Validate and process text
                var processedText = ValidateAndProcessText(text, requestId);

                // Use extension method GenerateEmbeddingAsync for single value
                var embedding = await _embeddingService.GenerateEmbeddingAsync(
                    processedText,
                    cancellationToken: cancellationToken);

                _logger.LogDebug("[{RequestId}] OpenAI embedding generated successfully. Dimensions: {Dimensions}",
                    requestId, embedding.Length);

                // Convert ReadOnlyMemory<float> to float[] and wrap in Embedding<float>
                return new Embedding<float>(embedding.ToArray());
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[{RequestId}] OpenAI embedding generation was cancelled", requestId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error generating OpenAI embedding via Semantic Kernel", requestId);
                throw new InvalidOperationException($"Failed to generate embedding: {ex.Message}", ex);
            }
        }

        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> texts,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var textList = texts.ToList();
            var requestId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogInformation("[{RequestId}] Generating batch OpenAI embeddings for {Count} texts via Semantic Kernel",
                requestId, textList.Count);

            if (textList.Count == 0)
            {
                return new GeneratedEmbeddings<Embedding<float>>(new List<Embedding<float>>());
            }

            // Validate batch size
            if (textList.Count > MAX_BATCH_SIZE)
            {
                _logger.LogWarning("[{RequestId}] Batch size {Count} exceeds limit {Limit}, processing in chunks",
                    requestId, textList.Count, MAX_BATCH_SIZE);
                return await ProcessLargeBatchAsync(textList, requestId, cancellationToken);
            }

            try
            {
                // Process and validate texts
                var processedTexts = textList.Select((text, index) =>
                    ValidateAndProcessText(text, $"{requestId}-{index}")).ToList();

                // Use GenerateEmbeddingsAsync for multiple values
                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                    processedTexts,
                    cancellationToken: cancellationToken);

                var result = embeddings.Select(e => new Embedding<float>(e.ToArray())).ToList();

                _logger.LogInformation("[{RequestId}] Batch OpenAI embedding completed via Semantic Kernel. Generated: {Count}",
                    requestId, result.Count);

                return new GeneratedEmbeddings<Embedding<float>>(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error in batch embedding generation, falling back to individual processing", requestId);

                // Fallback to individual processing if batch fails
                return await GenerateIndividuallyAsync(textList, requestId, cancellationToken);
            }
        }

        private async Task<GeneratedEmbeddings<Embedding<float>>> ProcessLargeBatchAsync(
            List<string> texts,
            string requestId,
            CancellationToken cancellationToken)
        {
            var allEmbeddings = new List<Embedding<float>>();
            var chunks = ChunkList(texts, MAX_BATCH_SIZE);

            foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
            {
                _logger.LogDebug("[{RequestId}] Processing chunk {Index}/{Total} with {Count} items",
                    requestId, index + 1, chunks.Count, chunk.Count);

                var chunkResult = await GenerateAsync(chunk, null, cancellationToken);
                allEmbeddings.AddRange(chunkResult);

                // Add small delay between chunks to respect rate limits
                if (index < chunks.Count - 1)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            return new GeneratedEmbeddings<Embedding<float>>(allEmbeddings);
        }

        private async Task<GeneratedEmbeddings<Embedding<float>>> GenerateIndividuallyAsync(
            List<string> texts,
            string requestId,
            CancellationToken cancellationToken)
        {
            var embeddings = new List<Embedding<float>>();
            var failedCount = 0;

            // Process with controlled concurrency
            using var semaphore = new SemaphoreSlim(MAX_CONCURRENCY);

            var tasks = texts.Select(async (text, index) =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var embedding = await GenerateAsync(text, cancellationToken);
                    return (Index: index, Embedding: embedding, Success: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{RequestId}] Failed to generate embedding for item {Index}", requestId, index);
                    Interlocked.Increment(ref failedCount);
                    return (Index: index, Embedding: default(Embedding<float>), Success: false);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);

            embeddings.AddRange(results
                .Where(r => r.Success)
                .OrderBy(r => r.Index)
                .Select(r => r.Embedding));

            _logger.LogInformation("[{RequestId}] Individual batch processing completed. Success: {Success}, Failed: {Failed}",
                requestId, embeddings.Count, failedCount);

            return new GeneratedEmbeddings<Embedding<float>>(embeddings);
        }

        private string ValidateAndProcessText(string text, string requestId)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Text cannot be null or empty");
            }

            // Truncate if necessary for OpenAI limits
            if (text.Length > MAX_TEXT_LENGTH)
            {
                _logger.LogWarning("[{RequestId}] Text length {Length} exceeds OpenAI limit, truncating to {Limit}",
                    requestId, text.Length, MAX_TEXT_LENGTH);
                text = text.Substring(0, MAX_TEXT_LENGTH);
            }

            // Basic cleanup
            text = text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Text is empty after processing");
            }

            return text;
        }

        private static List<List<T>> ChunkList<T>(List<T> source, int chunkSize)
        {
            var chunks = new List<List<T>>();
            for (int i = 0; i < source.Count; i += chunkSize)
            {
                chunks.Add(source.Skip(i).Take(chunkSize).ToList());
            }
            return chunks;
        }

        public object? GetService(Type serviceType, object? context = null)
        {
            if (serviceType == typeof(ITextEmbeddingGenerationService))
                return _embeddingService;

            return null;
        }

        public void Dispose()
        {
            // Semantic Kernel services tự quản lý lifecycle
        }
    }
}
