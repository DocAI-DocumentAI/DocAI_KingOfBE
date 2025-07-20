using AI.API.Services.Implement;
using AI.API.Services.Interface;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Implement;
using AI.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.TextGeneration;
using Polly;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Polly.Extensions.Http;
using Microsoft.SemanticKernel.Connectors.HuggingFace;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using static OllamaSharp.OllamaApiClient;

namespace AI.API.Extensions
{
    public static class DependencyService
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services)
        {
            IConfiguration configuration = new ConfigurationBuilder()
           .SetBasePath(Directory.GetCurrentDirectory())
           .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
           .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<DocAIDbContext>(options =>
                options.UseNpgsql(connectionString, builder =>
                {
                    builder.MigrationsAssembly(typeof(DocAIDbContext).Assembly.GetName().Name);
                    builder.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
                }));

            services.AddScoped<DbContext, DocAIDbContext>();
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IUnitOfWork<DocAIDbContext>, UnitOfWork<DocAIDbContext>>();

            return services;
        }
        public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
        {

            services.AddHttpClient<IAIService, AIService>()
                  .AddPolicyHandler(GetRetryPolicy())
                  .AddPolicyHandler(GetCircuitBreakerPolicy());

            var kernelBuilder = Kernel.CreateBuilder();

            string textModel = configuration["HuggingFace:TextModel"];
            string textEndpoint = configuration["HuggingFace:TextEndpoint"];
            string embeddingModel = configuration["HuggingFace:EmbeddingModel"];
            string embeddingEndpoint = configuration["HuggingFace:EmbeddingEndpoint"];
            string apiKey = configuration["HuggingFace:ApiKey"];



            kernelBuilder.Services.AddSingleton<IChatCompletionService>(sp =>
                            new HuggingFaceChatCompletionService(
                                endpoint: new Uri(textEndpoint),
                                apiKey: apiKey,
                                httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
                                loggerFactory: sp.GetService<ILoggerFactory>()
                            ));

            kernelBuilder.Services.AddHuggingFaceEmbeddingGenerator(
                model: embeddingModel,
                endpoint: new Uri(embeddingEndpoint),
                apiKey: apiKey,
                serviceId: "hf-embed-gen"
            );

            services.AddSingleton(kernelBuilder.Build());




            services.AddScoped<IAIService, AIService>();
            services.AddScoped<IConfigurationService, ConfigurationService>();
            services.AddScoped<IPromptTemplateService, PromptTemplateService>();
            services.AddScoped<IMetricsService, MetricsService>();
            //services.AddScoped<IHealthCheckService, HealthCheckService>();
            services.AddSingleton<ICacheService, CacheService>();
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = 1024 * 1024 * 100; // 100MB
            });

            return services;
        }
        public static IServiceCollection AddRateLimit(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("api", options =>
                {
                    options.PermitLimit = 100;
                    options.Window = TimeSpan.FromMinutes(1);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 10;
                });

                options.AddSlidingWindowLimiter("ai-generation", options =>
                {
                    options.PermitLimit = 10;
                    options.Window = TimeSpan.FromMinutes(1);
                    options.SegmentsPerWindow = 4;
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 5;
                });
            });
            return services;
        }
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        if (context.TryGetValue("logger", out var loggerObj) && loggerObj is ILogger logger)
                        {
                            logger.LogWarning("Retry {RetryCount} after {TimeSpan}s", retryCount, timespan.TotalSeconds);
                        }
                    });
        }
        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(30));
        }
    }
}
