using System;
using System.Security.Claims;
using System.Text;
using ChatBox.API.Mappers;
using ChatBox.API.Middlewares;
using ChatBox.API.Plugins;
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
                      var logger = serviceProvider.GetService<ILogger<DocumentSearchService>>();
                      logger?.LogWarning("Retrying HTTP request. Attempt {RetryAttempt} after {Timespan} due to {Reason}",
                          retryAttempt, timespan, outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase);
                  });
    }
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase();
        services.AddUnitOfWork();

        services.AddHttpContextAccessor();
        services.AddAutoMapper(typeof(MappingProfile).Assembly);

        services.AddMemoryCache();
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "ChatBox";
            });
        }

        services.AddHttpClient<IDocumentSearchService, DocumentSearchService>(client =>
        {
            var documentServiceUrl = configuration["ChatService:DocumentMicroserviceBaseUrl"];
            if (!string.IsNullOrEmpty(documentServiceUrl))
            {
                client.BaseAddress = new Uri(documentServiceUrl);
            }
            var timeout = configuration.GetValue("ChatService:RequestTimeoutSeconds", 120);
            client.Timeout = TimeSpan.FromSeconds(timeout);
        })
             .AddPolicyHandler((serviceProvider, request) => GetRetryPolicy(serviceProvider));

        // Application services
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        services.AddScoped<ITokenCountService, TokenCountService>();
        services.AddScoped<IContentFilterService, ContentFilterService>();
        services.AddScoped<IPreferenceService, PreferenceService>();
        services.AddScoped<IUserPreferenceService, UserPreferenceService>();
        services.AddScoped<IAdminService, AdminService>();

        // Semantic Kernel plugins
        services.AddScoped<DocumentSearchPlugin>();
        services.AddScoped<TimePlugin>();

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