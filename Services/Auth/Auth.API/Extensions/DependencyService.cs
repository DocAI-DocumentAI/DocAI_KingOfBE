using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using Auth.API.Consumers;
using Auth.API.Services.Implement;
using Auth.API.Services.Interface;
using Auth.Domain.Models;
using Auth.Infrastructure.Repository.Implement;
using Auth.Infrastructure.Repository.Interfaces;
using DOCA.API.Services.Implement;
using MassTransit;
// using Auth.API.Services.Implement;
// using Auth.API.Services.Interface;
// using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Auth.API.Extensions;

public static class DependencyService
{
    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork<DocAIAuthContext>, UnitOfWork<DocAIAuthContext>>();
        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<DocAIAuthContext>(options =>
            options.UseNpgsql(connectionString, b => b.MigrationsAssembly("Auth.API")));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<DbContext, DocAIAuthContext>();

        return services;
    }

    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrEmpty(redisConnectionString))
        {
            throw new InvalidOperationException(" Connection string cho Redis không được cấu hình.");
        }

        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddScoped<IRedisService, RedisService>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IActiveKeyService, ActiveKeyService>();
        services.AddScoped<IRedisService, RedisService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<Services.Interface.IAuthorizationService, Services.Implement.AuthorizationService>();
        services.AddSingleton<IPublishEndpoint, MockPublishEndpoint>();

        // Bỏ comment phần MassTransit
        services.AddMassTransit(x =>
        {
            x.AddConsumer<UserRequestMessageConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMQ:Host"] ?? "localhost";
                var username = configuration["RabbitMQ:Username"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";

                cfg.Host(host, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                // Đảm bảo endpoint name đúng với endpoint mà bạn publish message đến
                cfg.ReceiveEndpoint("user-request-queue", e =>
                {
                    e.ConfigureConsumer<UserRequestMessageConsumer>(context);
                    // Thêm retry để xử lý lỗi
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    // Thêm log để debug
                    e.UseInMemoryOutbox();
                });

            });
        });
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

                // ⚠️ Thêm dòng này để hỗ trợ [Authorize(Roles = "...")]
                RoleClaimType = ClaimTypes.Role,

                // Nếu bạn dùng custom claim name như "roles", bạn có thể chỉnh ở đây
                // RoleClaimType = "roles",
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
