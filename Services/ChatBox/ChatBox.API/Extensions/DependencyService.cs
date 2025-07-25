using System;
using System.Security.Claims;
using System.Text;
using ChatBox.API.Mappers;
using ChatBox.API.Services.Implement;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Implement;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using Serilog;

namespace ChatBox.API.Extensions;

public static class DependencyService
{
    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork<ChatBoxDbContext>, UnitOfWork<ChatBoxDbContext>>();
        return services;
    }
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<ChatBoxDbContext>(options =>
                options.UseNpgsql(connectionString, builder =>
                {
                    builder.MigrationsAssembly(typeof(ChatBoxDbContext).Assembly.GetName().Name);
                }));

            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<DbContext, ChatBoxDbContext>();

            return services;
    }
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(IServiceProvider serviceProvider)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var logger = serviceProvider.GetService<ILogger<AiServiceClient>>();
                    logger?.LogWarning("Retrying HTTP request. Attempt {RetryAttempt} after {Timespan} due to {Reason}",
                        retryAttempt, timespan, outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase);
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(IServiceProvider serviceProvider)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30),
                onBreak: (outcome, timespan, context) =>
                {
                    var logger = serviceProvider.GetService<ILogger<AiServiceClient>>();
                    logger?.LogError("Circuit breaker opened for {Timespan} due to {Reason}",
                        timespan, outcome.Exception?.Message);
                },
                onReset: context =>
                {
                    var logger = serviceProvider.GetService<ILogger<AiServiceClient>>();
                    logger?.LogInformation("Circuit breaker reset");
                });
    }

    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase();
        services.AddUnitOfWork();

        services.AddHttpClient<IAiServiceClient, AiServiceClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["ChatService:AIMicroserviceBaseUrl"]
                ?? throw new InvalidOperationException("AI Microservice Base URL is missing."));
        })
             .SetHandlerLifetime(TimeSpan.FromMinutes(5))
             .AddPolicyHandler((serviceProvider, request) => GetRetryPolicy(serviceProvider))
             .AddPolicyHandler((serviceProvider, request) => GetCircuitBreakerPolicy(serviceProvider));

        services.AddHttpContextAccessor();
        services.AddAutoMapper(typeof(MappingProfile).Assembly);

        services.AddHttpClient<IDocumentServiceClient, DocumentServiceClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["ChatService:DocumentMicroserviceBaseUrl"] ?? throw new InvalidOperationException("Document Microservice Base URL is missing."));
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        // Register new HTTP clients for new services
        services.AddHttpClient<IAiServiceClient, AiServiceClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["ChatService:AIMicroserviceBaseUrl"]
                ?? throw new InvalidOperationException("AI Microservice Base URL is missing."));
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler((serviceProvider, request) => GetRetryPolicy(serviceProvider))
        .AddPolicyHandler((serviceProvider, request) => GetCircuitBreakerPolicy(serviceProvider));

        services.AddHttpClient<IDocumentServiceClient, ChatBox.API.Services.Implement.DocumentServiceClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["ChatService:DocumentMicroserviceBaseUrl"]
                ?? throw new InvalidOperationException("Document Microservice Base URL is missing."));
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler((serviceProvider, request) => GetRetryPolicy(serviceProvider))
        .AddPolicyHandler((serviceProvider, request) => GetCircuitBreakerPolicy(serviceProvider));

        // Keep legacy services for backward compatibility
        services.AddScoped<IChatService, ChatService>();

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

}