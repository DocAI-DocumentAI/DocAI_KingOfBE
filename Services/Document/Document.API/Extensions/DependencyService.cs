
﻿using Document.API.Consumers;
using MassTransit;
﻿using Document.API.Services.Implements;
using Document.API.Services.Interfaces;
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





namespace Document.API.Extensions;

public static class DependencyService
{

    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<UserRequestMessageConsumer>(); 
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
            });
        });
        services.AddScoped<IAzureStorageService, AzureStorageService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IBookmarkService, BookmarkService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<ITagService, TagService>();
        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
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
            Endpoint = openRouterConfig.Endpoint
        };

        var openAITextEmbeddingConfig = new OpenAIConfig
        {
            EmbeddingModel = openAIConfig.EmbeddingModel,
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
            .Build<MemoryServerless>(kmbOptions);

        services.AddSingleton<IKernelMemory>(memory);

        return services;
    }


    //public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    //{
    //    var redisConnectionString = configuration.GetConnectionString("Redis");

    //    if (string.IsNullOrEmpty(redisConnectionString))
    //    {
    //        throw new InvalidOperationException(" Connection string cho Redis không được cấu hình.");
    //    }

    //    services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
    //    services.AddScoped<IRedisService, RedisService>();

    //    return services;
    //}


}