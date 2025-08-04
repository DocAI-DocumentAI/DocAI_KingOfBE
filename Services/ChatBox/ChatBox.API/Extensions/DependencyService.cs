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
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.DTOs;

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
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase();
        services.AddUnitOfWork();

        services.AddHttpContextAccessor();
        services.AddAutoMapper(typeof(MappingProfile).Assembly);

        services.AddMemoryCache();

        // Application services
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ISemanticKernelService, SemanticKernelService>();
        services.AddScoped<ITokenCountService, TokenCountService>();
        services.AddScoped<IPreferenceService, PreferenceService>();
        services.AddScoped<IDocumentSearchService, DocumentSearchService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IManualDocumentSearchService, ManualDocumentSearchService>();

        // Semantic Kernel plugins
        services.AddScoped<DocumentSearchPlugin>();
        services.AddScoped<TimePlugin>();

        return services;
    }
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        string secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is missing in configuration.");
        if (secret.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Secret must be at least 32 characters long for HS256.");
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
                ValidIssuer = configuration["Jwt:Issuer"] ?? "DocAI",
                IssuerSigningKey = new SymmetricSecurityKey(key),
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.NameIdentifier
            };

            // Debug JWT events
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var claims = context.Principal.Claims.Select(c => $"{c.Type}: {c.Value}");
                    Log.Information("JWT validated with claims: {Claims}", string.Join(", ", claims));
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    Log.Warning("JWT authentication failed: {Error}", context.Exception?.Message);
                    return Task.CompletedTask;
                },
                OnMessageReceived = context =>
                {
                    var token = context.Request.Headers["Authorization"].FirstOrDefault();
                    return Task.CompletedTask;
                }
            };
        });
            
        return services;
    }
    public static IServiceCollection AddRabbitmq(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.AddRequestClient<ChatBoxDocumentRequest>(new Uri("queue:document.search.request"));

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqConfig = configuration.GetSection("RabbitMQ");
                cfg.Host(rabbitMqConfig["Host"], h =>
                {
                    h.Username(rabbitMqConfig["Username"]);
                    h.Password(rabbitMqConfig["Password"]);
                });

                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });
        });

        return services;
    }
}