using AI.API.Services.Implement;
using AI.API.Services.Interface;
using AI.API.Services;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Implement;
using AI.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextGeneration;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using AutoMapper;
using AI.API.Mappers;
using AI.API.Background;
using Polly;
using Polly.Extensions.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Serilog;
using System.Data.Common;

using AI.API.Payload.Response;
using Microsoft.SemanticKernel.Embeddings;

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
                    builder.MigrationsAssembly("AI.Domain");
                    builder.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
                }));

            services.AddScoped<DbContext, DocAIDbContext>();
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IUnitOfWork<DocAIDbContext>, UnitOfWork<DocAIDbContext>>();

            return services;
        }
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Core application services
            services.AddScoped<IAIConfigurationService, ConfigurationService>();
            services.AddScoped<IMetricsService, MetricsService>();
            services.AddScoped<IAIService, AIService>();
            services.AddScoped<IDynamicProviderService, DynamicProviderService>();
            services.AddScoped<IProviderFactory, ProviderFactory>();
            services.AddScoped<IKernelProviderService, KernelProviderService>();
            services.AddScoped<IAIModelConfigService, AIModelConfigService>();

            // Add Health Checks
            services.AddHealthChecks()
                .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

            // Security services
            services.Configure<AI.API.Security.InputSanitizationOptions>(configuration.GetSection("InputSanitization"));
            services.AddScoped<AI.API.Security.InputSanitizer>();

            // Caching services
            services.AddSingleton<ICacheService, CacheService>();
            services.AddMemoryCache();

            return services;
        }
        public static IServiceCollection AddOpenAIEmbeddingService(this IServiceCollection services, IConfiguration configuration)
        {
            var openAiApiKey = configuration["AI:Embedding:OpenAI:ApiKey"];
            var embeddingModel = configuration["AI:Embedding:OpenAI:Model"] ?? "text-embedding-3-small";
            var dimensions = configuration.GetValue<int?>("AI:Embedding:OpenAI:Dimensions");
            var orgId = configuration["AI:Embedding:OpenAI:OrganizationId"]; // Optional

            if (string.IsNullOrWhiteSpace(openAiApiKey))
            {
                throw new InvalidOperationException("AI:Embedding:OpenAI:ApiKey is required in configuration for embedding service.");
            }
#pragma warning disable SKEXP0010

            // Use official Semantic Kernel dependency injection method
            services.AddOpenAITextEmbeddingGeneration(
                modelId: embeddingModel,
                apiKey: openAiApiKey
            );

            // Create an adapter that implements IEmbeddingGenerator<string, Embedding<float>>
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider =>
            {
                var embeddingService = serviceProvider.GetRequiredService<ITextEmbeddingGenerationService>();
                var logger = serviceProvider.GetRequiredService<ILogger<SemanticKernelEmbeddingAdapter>>();

                return new SemanticKernelEmbeddingAdapter(embeddingService, logger);
            });

            return services;
        }
        public static IServiceCollection AddHuggingFaceServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure HttpClient with retry policies
            services.AddHttpClient("HuggingFaceClient", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.Add("User-Agent", "DocAI-AI-Service/1.0");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            // HuggingFace configuration validation (for fallback service only)
            var hfApiKey = configuration["AI:FallbackProvider:HuggingFace:ApiKey"];
            var hfChatModel = configuration["AI:FallbackProvider:HuggingFace:Model"];
            var hfEndpoint = configuration["AI:FallbackProvider:HuggingFace:Endpoint"] ?? "https://router.huggingface.co/v1/chat/completions";

            if (string.IsNullOrWhiteSpace(hfApiKey))
            {
                throw new InvalidOperationException("AI:FallbackProvider:HuggingFace:ApiKey is required in configuration for fallback service.");
            }

            if (string.IsNullOrWhiteSpace(hfChatModel))
            {
                throw new InvalidOperationException("AI:FallbackProvider:HuggingFace:Model is required in configuration for fallback service.");
            }

            // Register DEFAULT/FALLBACK HuggingFace Text Generation Service
            // This will be used when no dynamic configuration is available
            services.AddSingleton<ITextGenerationService>(serviceProvider =>
            {
                var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("HuggingFaceClient");
                var logger = serviceProvider.GetRequiredService<ILogger<HuggingFaceTextService>>();

                return new HuggingFaceTextService(
                    httpClient: httpClient,
                    apiKey: hfApiKey,
                    model: hfChatModel,
                    endpoint: hfEndpoint,
                    logger: logger
                );
            });

            // Register Dynamic Provider Services for creating services on-demand
            services.AddScoped<IDynamicProviderService, DynamicProviderService>();

            return services;
        }
        public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
        {
            // Create Semantic Kernel with both fixed embedding and dynamic text generation
            services.AddSingleton<Kernel>(serviceProvider =>
            {
                var kernelBuilder = Kernel.CreateBuilder();

                // Add default text generation service (HuggingFace - will be overridden by dynamic service)
                var textService = serviceProvider.GetRequiredService<ITextGenerationService>();
                kernelBuilder.Services.AddSingleton(textService);

                // Add fixed embedding service (OpenAI)
                var embeddingService = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
                kernelBuilder.Services.AddSingleton(embeddingService);

                return kernelBuilder.Build();
            });

            return services;
        }
        public static IServiceCollection AddAutoMapperProfiles(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(MappingProfile));

            // Register PaginateConverter for AutoMapper
            services.AddSingleton(provider => new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
                cfg.ConstructServicesUsing(type => provider.GetService(type));
            }).CreateMapper());

            return services;
        }

        // Replaced by SimpleRateLimitMiddleware
        /*public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            var rateLimitConfig = configuration.GetSection("RateLimiting");

            services.AddRateLimiter(options =>
            {
                // General API rate limiting
                options.AddFixedWindowLimiter("api", limiterOptions =>
                {
                    limiterOptions.PermitLimit = rateLimitConfig.GetValue<int>("PermitLimit", 100);
                    limiterOptions.Window = TimeSpan.Parse(rateLimitConfig.GetValue<string>("Window", "00:01:00"));
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = rateLimitConfig.GetValue<int>("QueueLimit", 10);
                });

                // AI text generation rate limiting
                options.AddSlidingWindowLimiter("ai-generation", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 10;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.SegmentsPerWindow = 4;
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 5;
                });

                // AI streaming rate limiting
                options.AddSlidingWindowLimiter("ai-stream", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 5;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.SegmentsPerWindow = 4;
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 2;
                });

                // Embedding generation rate limiting
                options.AddFixedWindowLimiter("embeddings", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 50;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 10;
                });

                // Batch embedding rate limiting
                options.AddFixedWindowLimiter("embeddings-batch", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 10;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 5;
                });

                // Admin endpoints rate limiting
                options.AddFixedWindowLimiter("admin", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 50;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 5;
                });

                // Global rejection behavior
                options.RejectionStatusCode = 429;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Request.Headers.Host.ToString(),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 1000,
                            Window = TimeSpan.FromMinutes(1)
                        }));
            });

            return services;
        }*/
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            string secret = configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret is missing in configuration.");
            if (secret.Length < 32)
            {
                throw new InvalidOperationException("JWT:Secret must be at least 32 characters long for HS256.");
            }

            var key = Encoding.UTF8.GetBytes(secret);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["JWT:Issuer"] ?? "DocAI",
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    RoleClaimType = ClaimTypes.Role,
                };

                options.SaveToken = true;

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        Log.Information("Token received: {Token}", context.Token);
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        Log.Error("JWT authentication failed: {Message}", context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        Log.Information("JWT token validated successfully. Claims: {Claims}",
                            string.Join(", ", context.Principal.Claims.Select(c => $"{c.Type}: {c.Value}")));
                        return Task.CompletedTask;
                    }
                };
            });

            return services;
        }
        public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
        {
            services.AddHostedService<MetricsCleanupService>();
            return services;
        }

        public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
        {
            var corsSection = configuration.GetSection("Cors");
            var allowedOrigins = corsSection.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    if (allowedOrigins.Any())
                    {
                        policy.WithOrigins(allowedOrigins);
                    }
                    else
                    {
                        policy.AllowAnyOrigin();
                    }

                    policy.AllowAnyMethod()
                          .AllowAnyHeader()
                          .WithExposedHeaders("X-Correlation-Id", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset");
                });

                // Development policy for local testing
                options.AddPolicy("Development", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            return services;
        }

        #region Private Helper Methods

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
                        if (context.TryGetValue("logger", out var loggerObj) && loggerObj is Microsoft.Extensions.Logging.ILogger logger)
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

        #endregion
    }

    #region Helper Classes

    /// <summary>
    /// Dummy embedding generator when no embedding model is configured
    /// </summary>
    internal class DummyEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<Embedding<float>> GenerateAsync(string text, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Embedding model is not configured. Please set HuggingFace:EmbeddingModel in appsettings.json");
        }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> texts, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Embedding model is not configured. Please set HuggingFace:EmbeddingModel in appsettings.json");
        }

        public object? GetService(Type serviceType, object? context = null)
        {
            return null;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    /// <summary>
    /// Extension methods for Polly context
    /// </summary>
    public static class PolicyContextExtensions
    {
        public static Microsoft.Extensions.Logging.ILogger GetLogger(this Polly.Context context)
        {
            if (context.TryGetValue("logger", out var loggerObj) && loggerObj is Microsoft.Extensions.Logging.ILogger logger)
            {
                return logger;
            }
            return null;
        }
    }

    #endregion
}
