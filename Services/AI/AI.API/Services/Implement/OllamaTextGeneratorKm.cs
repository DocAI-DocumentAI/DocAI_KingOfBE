using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration; 
using OllamaSharp; 
using OllamaSharp.Models; 
using OllamaSharp.Models.Chat; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tiktoken;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory;
using Microsoft.Extensions.AI;

namespace AI.API.Services.Implement
{
    public class OllamaTextGeneratorKm : ITextGenerator
    {
        private readonly ILogger<OllamaTextGeneratorKm> _log;
        private readonly OllamaApiClient _ollamaClient;
        private readonly string _textGenerationModelName;
        private readonly IConfiguration _configuration;
        private readonly ITextTokenizer _textTokenizer;
        public int MaxTokenTotal { get; }

        public OllamaTextGeneratorKm(
    ILogger<OllamaTextGeneratorKm> logger,
    IConfiguration configuration,
    ITextTokenizer? textTokenizer = null) // Inject ITextTokenizer (CL100KTokenizer)
        {
            _log = logger;
            _configuration = configuration;

            var ollamaHost = _configuration.GetValue<string>("Ollama:Host") ?? throw new InvalidOperationException("Ollama:Host configuration is missing.");
            _textGenerationModelName = _configuration.GetValue<string>("Ollama:TextGenerationModel") ?? throw new InvalidOperationException("Ollama:TextGenerationModel configuration is missing.");

            _ollamaClient = new OllamaApiClient(ollamaHost, _textGenerationModelName);

            _textTokenizer = textTokenizer ?? new Microsoft.KernelMemory.AI.CL100KTokenizer(); // REVIEW POINT: Sử dụng CL100KTokenizer
            if (_textTokenizer == null)
            {
                _log.LogWarning("Text tokenizer not specified for OllamaTextGeneratorKm, token counts might be inaccurate.");
            }

            MaxTokenTotal = _configuration.GetValue<int>("Ollama:TextMaxTokenTotal", 4096);
            if (MaxTokenTotal <= 0) MaxTokenTotal = 4096;

            _log.LogInformation("OllamaTextGeneratorKm initialized. Host: {Host}, Model: {Model}, MaxTokenTotal: {MaxTokenTotal}", ollamaHost, _textGenerationModelName, MaxTokenTotal);

            Task.Run(async () =>
            {
                try
                {
                    _log.LogInformation("Attempting to connect to Ollama server for text generation...");
                    if (!await _ollamaClient.IsRunningAsync())
                    {
                        _log.LogWarning("Ollama server not reachable for text generation.");
                    }
                    else
                    {
                        var models = await _ollamaClient.ListLocalModelsAsync();
                        if (!models.Any(m => m.Name.Equals(_textGenerationModelName, StringComparison.OrdinalIgnoreCase)))
                        {
                            _log.LogError($"Text generation model '{_textGenerationModelName}' not found on Ollama server. Please pull it using 'ollama pull {_textGenerationModelName}'.");
                            throw new InvalidOperationException($"Text generation model '{_textGenerationModelName}' is missing.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to initialize OllamaTextGeneratorKm.");
                    throw;
                }
            }).Wait();
        }
        public int CountTokens(string text)
        {
            return _textTokenizer.CountTokens(text);
        }
        public async IAsyncEnumerable<GeneratedTextContent> GenerateTextAsync(
             string prompt,
             TextGenerationOptions options, // Đây là options của KM
             [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _log.LogTrace("Generating text with model {ModelName}, prompt length {PromptLength} tokens", _textGenerationModelName, CountTokens(prompt));

            // Ánh xạ TextGenerationOptions của KM sang RequestOptions của OllamaSharp
            // Lấy các giá trị từ appsettings.json nếu KM không cung cấp trong options
            var temperature = _configuration.GetValue<float>("Ollama:Temperature", (float)options.Temperature);
            var topP = _configuration.GetValue<float>("Ollama:TopP", (float)options.NucleusSampling);
            var numPredict = options.MaxTokens ?? _configuration.GetValue<int>("Ollama:NumPredict", 1024);

            var ollamaOptions = new RequestOptions
            {
                Temperature = temperature,
                TopP = topP,
                NumPredict = numPredict,
                // Có thể thêm các tùy chọn khác từ appsettings.json vào đây nếu bạn muốn kiểm soát chúng qua KM
                // Ví dụ: MiroStat, Seed, StopSequences...
                // MiroStat = _configuration.GetValue<int?>("Ollama:MiroStat"),
                // Seed = _configuration.GetValue<int?>("Ollama:Seed"),
                // Stop = options.StopSequences?.ToArray() // Nếu TextGenerationOptions của KM có StopSequences
            };

            var chatRequest = new GenerateRequest
            {
                Model = _textGenerationModelName,
                Prompt = prompt, // `ITextGenerator` nhận `prompt` string
                Stream = true, // Luôn streaming
                Options = ollamaOptions
            };

            //try
            //{
                // REVIEW POINT: Sử dụng GenerateAsync (completion API) của OllamaSharp
                // KM's ITextGenerator thường được sử dụng cho cả chat và completion.
                // GenerateAsync của OllamaSharp là cho text completion (từ prompt).
                await foreach (var chunk in _ollamaClient.GenerateAsync(chatRequest, cancellationToken).ConfigureAwait(false))
                {
                    if (chunk?.Response != null) // GenerateResponseStream trả về `Response`, không phải `Message.Content`
                    {
                        yield return new GeneratedTextContent(chunk.Response); // Wrap vào GeneratedTextContent
                    }
                }
            //}
            //catch (Exception ex)
            //{
            //    _log.LogError(ex, "Error generating text with Ollama model {ModelName}", _textGenerationModelName);
            //    throw new KernelMemoryException($"Failed to generate text with Ollama: {ex.Message}", ex);
            //}
        }

        public IReadOnlyList<string> GetTokens(string text)
        {
            return _textTokenizer.GetTokens(text);
        }
    }
}
