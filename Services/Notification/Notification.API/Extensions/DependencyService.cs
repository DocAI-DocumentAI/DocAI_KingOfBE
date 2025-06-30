using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notification.API.Consumers;
using Notification.Infrastructure.Repository.Implement;
using Notification.Infrastructure.Repository.Interfaces;
using Serilog;

namespace Auth.API.Extensions;

public static class DependencyService
{
    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {
        // services.AddScoped<IUnitOfWork<DocAIAuthContext>, UnitOfWork<DocAIAuthContext>>();
        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // services.AddDbContext<DocAIAuthContext>(options =>
        //     options.UseNpgsql(connectionString, b => b.MigrationsAssembly("Notification.API")));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        // services.AddScoped<DbContext, DocAIAuthContext>();

        return services;
    }

    // public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    // {
    //     var redisConnectionString = configuration.GetConnectionString("Redis");
    //
    //     if (string.IsNullOrEmpty(redisConnectionString))
    //     {
    //         throw new InvalidOperationException(" Connection string cho Redis không được cấu hình.");
    //     }
    //
    //     services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
    //     services.AddScoped<IRedisService, RedisService>();
    //
    //     return services;
    // }

    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        // services.AddScoped<IUserService, UserService>();
        services.AddMassTransit(x =>
        {
            x.AddConsumer<UserRequestMessageConsumer>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host("rabbitmq", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                cfg.ReceiveEndpoint("user-request-queue", e =>
                {
                    e.ConfigureConsumer<UserRequestMessageConsumer>(context);
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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
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
                        Log.Information("JWT token validated successfully. Claims: {Claims}", context.Principal.Claims.Select(c => $"{c.Type}: {c.Value}"));
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

}
