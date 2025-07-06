
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
using Microsoft.KernelMemory.AI.Ollama;
using Shared.DTOs;

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
                cfg.Host("rabbitmq://localhost", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
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

    public static IServiceCollection AddKernelMemoryOllama(this IServiceCollection services, IConfiguration configuration)
    {
        var ollamaConfigSettings = configuration.GetSection("Ollama").Get<OllamaConfigSetting>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Prepare the configuration for Kernel Memory using the bound settings
        var ollamaConfig = new OllamaConfig
        {
            Endpoint = ollamaConfigSettings.Endpoint,
            TextModel = new OllamaModelConfig(ollamaConfigSettings.TextModel, 131072),
            EmbeddingModel = new OllamaModelConfig(ollamaConfigSettings.EmbeddingModel, 2048)
        };

        // Prepare Postgres/pgvector configuration
        var postgresConfig = new PostgresConfig
        {
            ConnectionString = connectionString
        };

        //quick test for temp file
        KernelMemoryBuilderBuildOptions kmbOptions = new()
        {
            AllowMixingVolatileAndPersistentData = true
        };

        // Build the Kernel Memory instance with Ollama services
        var memory = new KernelMemoryBuilder()

            .WithOllamaTextGeneration(ollamaConfig, new CL100KTokenizer())
            .WithOllamaTextEmbeddingGeneration(ollamaConfig, new CL100KTokenizer())
            .WithPostgresMemoryDb(postgresConfig)
            .Build<MemoryServerless>(kmbOptions);

        // Register the IKernelMemory instance as a singleton so it can be injected elsewhere
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