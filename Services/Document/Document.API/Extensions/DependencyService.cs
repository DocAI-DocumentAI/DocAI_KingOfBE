using Document.API.Consumers;
using Microsoft.KernelMemory.Configuration;

using MassTransit;
using Document.API.Services.Implements;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.Domain.Context;
using Document.Infrastructure.Repository.Implement;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory;

using Shared.DTOs;

using Document.API.Models;

using Microsoft.KernelMemory.AI.OpenAI;
using Microsoft.KernelMemory.SemanticKernel;
using Document.API.Configuration;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using static System.Net.WebRequestMethods;
using Shared.Command;





namespace Document.API.Extensions;

public static class DependencyService
{

    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Google Drive and Storage settings
        services.Configure<GoogleDriveConfiguration>(configuration.GetSection(GoogleDriveConfiguration.SectionName));
        services.Configure<StorageConfiguration>(configuration.GetSection(StorageConfiguration.SectionName));

        services.AddMassTransit(x =>
        {
            x.AddConsumer<UserRequestMessageConsumer>();
            x.AddConsumer<DocumentSearchConsumer>();
            x.AddConsumer<GetExpiringDocumentsConsumer>();
            x.AddConsumer<UpdateDocumentStatusConsumer>();
            x.AddConsumer<DeactivateDocumentWarningsConsumer>();

            // Google Drive permission setup consumers
            x.AddConsumer<SetupDepartmentGoogleDrivePermissionsConsumer>();
            x.AddConsumer<SetupUserGoogleDrivePermissionsConsumer>();
            x.AddConsumer<InitializeBulkGoogleDrivePermissionsConsumer>();

            // Add request client for name lookup
            x.AddRequestClient<NameLookupRequest>(new Uri("queue:name-lookup-queue"));

            // Add request clients for permission-related Auth service communication
            x.AddRequestClient<DepartmentEmployeeRequest>(new Uri("queue:department-employee-queue"));
            x.AddRequestClient<CompanyEmployeeRequest>(new Uri("queue:company-employee-queue"));
            x.AddRequestClient<GetAllDepartmentsRequest>(new Uri("queue:get-all-departments-queue"));
            x.AddRequestClient<UserEmailRequest>(new Uri("queue:user-email-queue"));
            x.AddRequestClient<GetDepartmentNamesCommand>(new Uri("queue:get-department-names-queue"));

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqConfig = configuration.GetSection("RabbitMQ");
                cfg.Host(rabbitMqConfig["Host"], h =>
                {
                    h.Username(rabbitMqConfig["Username"]);
                    h.Password(rabbitMqConfig["Password"]);
                });

                cfg.ReceiveEndpoint("user-request-queue", e =>
                {
                    // Chỉ định consumer nào sẽ xử lý message từ queue này
                    e.ConfigureConsumer<UserRequestMessageConsumer>(context);
                });
                //  ChatBox RAG endpoints
                cfg.ReceiveEndpoint("document.search.request", e =>
                {
                    e.ConfigureConsumer<DocumentSearchConsumer>(context);
                    e.ConcurrentMessageLimit = 10;
                    e.PrefetchCount = 20;
                    e.UseConcurrencyLimit(10);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
                });
                cfg.ReceiveEndpoint("document-expiring-queue", e =>
                {
                    e.ConfigureConsumer<GetExpiringDocumentsConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                cfg.ReceiveEndpoint("document-status-update-queue", e =>
                {
                    e.ConfigureConsumer<UpdateDocumentStatusConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                cfg.ReceiveEndpoint("document-warnings-deactivate-queue", e =>
                {
                    e.ConfigureConsumer<DeactivateDocumentWarningsConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                // Google Drive permission setup endpoints
                cfg.ReceiveEndpoint("googledrive-department-setup-queue", e =>
                {
                    e.ConfigureConsumer<SetupDepartmentGoogleDrivePermissionsConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(10)));
                    e.UseInMemoryOutbox();
                });

                cfg.ReceiveEndpoint("googledrive-user-setup-queue", e =>
                {
                    e.ConfigureConsumer<SetupUserGoogleDrivePermissionsConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                cfg.ReceiveEndpoint("googledrive-bulk-setup-queue", e =>
                {
                    e.ConfigureConsumer<InitializeBulkGoogleDrivePermissionsConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(2, TimeSpan.FromSeconds(30)));
                    e.UseInMemoryOutbox();
                });

                cfg.ConfigureEndpoints(context);

            });
        });

        // Storage services
        // Azure storage service commented out for Google Drive migration
        // services.AddScoped<IAzureStorageService, AzureStorageService>();
        services.AddScoped<IRedisService, RedisService>();
        services.AddScoped<IGoogleOAuthTokenService, GoogleOAuthTokenService>();
        services.AddScoped<IGoogleDriveOAuthService, GoogleDriveOAuthService>();
        services.AddScoped<IGoogleDriveService, GoogleDriveService>();
        services.AddScoped<IStorageService, UnifiedStorageService>();
        services.AddScoped<IMigrationService, MigrationService>();
        services.AddHttpClient<IGoogleDriveOAuthService, GoogleDriveOAuthService>();
        services.AddScoped<IFileConversionService, FileConversionService>();
        services.AddScoped<INameLookupService, NameLookupService>();
        services.AddScoped<IDocumentEnrichmentService, DocumentEnrichmentService>();
        services.AddScoped<AiResponseHelper>();
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentRecommendationService, DocumentRecommendationService>();
        services.AddScoped<IDocumentReplacementService, DocumentReplacementService>();
        services.AddScoped<IDocumentPermissionManager, DocumentPermissionManager>();
        services.AddScoped<ITokenUsageLogger, TokenUsageLogger>();
        services.AddScoped<IBookmarkService, BookmarkService>();
        services.AddScoped<IGoogleDrivePermissionSetupService, GoogleDrivePermissionSetupService>();
        // Background services
        services.AddHostedService<TokenRefreshBackgroundService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IDocumentTypeService, DocumentTypeService>();
        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        services.AddScoped<IDocumentRAGService, DocumentRAGService>();
        services.AddScoped<IDocumentNotificationService, DocumentNotificationService>();
        services.AddScoped<IDocumentExpirationService, DocumentExpirationService>();


        return services;
    }

    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {

        //services.AddScoped<IUnitOfWork<DocAIDocumentContext>, UnitOfWork<DocAIDocumentContext>>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<DocAIDocumentContext>(options =>
            options.UseNpgsql(connectionString, b => b.MigrationsAssembly("Document.API")));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<DbContext, DocAIDocumentContext>();

        return services;
    }

    //public static IServiceCollection AddKernelMemoryOllama(this IServiceCollection services, IConfiguration configuration)
    //{
    //    var ollamaConfigSettings = configuration.GetSection("Ollama").Get<OllamaConfigSetting>();
    //    var connectionString = configuration.GetConnectionString("DefaultConnection");

    //    // Prepare the configuration for Kernel Memory using the bound settings
    //    var ollamaConfig = new OllamaConfig
    //    {
    //        Endpoint = ollamaConfigSettings.Endpoint,
    //        TextModel = new OllamaModelConfig(ollamaConfigSettings.TextModel, 131072),
    //        EmbeddingModel = new OllamaModelConfig(ollamaConfigSettings.EmbeddingModel, 2048)
    //    };

    //    // Prepare Postgres/pgvector configuration
    //    var postgresConfig = new PostgresConfig
    //    {
    //        ConnectionString = connectionString
    //    };

    //    //quick test for temp file
    //    KernelMemoryBuilderBuildOptions kmbOptions = new()
    //    {
    //        AllowMixingVolatileAndPersistentData = true
    //    };

    //    // Build the Kernel Memory instance with Ollama services
    //    var memory = new KernelMemoryBuilder()

    //        .WithOllamaTextGeneration(ollamaConfig, new CL100KTokenizer())
    //        .WithOllamaTextEmbeddingGeneration(ollamaConfig, new CL100KTokenizer())
    //        .WithPostgresMemoryDb(postgresConfig)
    //        .Build<MemoryServerless>(kmbOptions);

    //    // Register the IKernelMemory instance as a singleton so it can be injected elsewhere
    //    services.AddSingleton<IKernelMemory>(memory);

    //    return services;
    //}

    public static IServiceCollection AddKernelMemory(this IServiceCollection services, IConfiguration configuration)
    {
        var config = new SemanticKernelConfig();

        var openRouterConfig = configuration.GetSection("OpenRouter").Get<OpenRouterConfigSetting>();
        var openAIConfig = configuration.GetSection("OpenAI").Get<OpenAIConfigSetting>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        var openRouterTextGenerationConfig = new OpenAIConfig
        {
            TextModel = openRouterConfig.Model,
            APIKey = openRouterConfig.APIKey,
            Endpoint = openRouterConfig.Endpoint,
            TextModelMaxTokenTotal = openRouterConfig.MaxTokens
        };

        var openAITextEmbeddingConfig = new OpenAIConfig
        {
            EmbeddingModel = openAIConfig.EmbeddingModel,
            Endpoint = "https://gpt1.shupremium.com/v1",
            APIKey = openAIConfig.APIKey
        };

        var postgresConfig = new PostgresConfig
        {
            ConnectionString = connectionString
        };

        KernelMemoryBuilderBuildOptions kmbOptions = new()
        {
            AllowMixingVolatileAndPersistentData = true
        };


        var memory = new KernelMemoryBuilder()
            .WithPostgresMemoryDb(postgresConfig)
            .WithOpenAITextGeneration(openRouterTextGenerationConfig, new CL100KTokenizer())
            .WithOpenAITextEmbeddingGeneration(openAITextEmbeddingConfig, new CL100KTokenizer())
            // .WithCustomTextPartitioningOptions(new TextPartitioningOptions
            // {
            //     MaxTokensPerParagraph = 100, // recommended for text-embedding-3 family
            //     OverlappingTokens = 200       // good balance of recall vs. cost
            // })
            .WithSearchClientConfig(new()
            {
                EmptyAnswer = "No results found. Please try again.",
                AnswerTokens = 1500,
                MaxMatchesCount = 30,
            })
            .Build<MemoryServerless>(kmbOptions);

        services.AddSingleton<IKernelMemory>(memory);

        return services;
    }


    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrEmpty(redisConnectionString))
        {
            throw new InvalidOperationException("Redis connection string is not configured.");
        }

        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

        return services;
    }

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

                // Đảm bảo role claim được map đúng
                RoleClaimType = ClaimTypes.Role,

                // Thêm claim mapping
                NameClaimType = ClaimTypes.NameIdentifier
            };

            // Debug JWT events
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var claims = context.Principal.Claims.Select(c => $"{c.Type}: {c.Value}");
                    Console.WriteLine($"JWT validated with claims: {string.Join(", ", claims)}");
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

}