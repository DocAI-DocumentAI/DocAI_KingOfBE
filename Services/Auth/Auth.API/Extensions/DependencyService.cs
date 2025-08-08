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
        // services.AddScoped<IActiveKeyService, ActiveKeyService>();
        services.AddScoped<IRedisService, RedisService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserPermissionService, UserPermissionService>();
        services.AddScoped<Services.Interface.IAuthorizationService, Services.Implement.AuthorizationService>();
        services.AddHttpClient<IGoogleOAuthService, GoogleOAuthService>();
        services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
        services.AddHttpClient<GoogleOAuthService>();
        services.AddScoped<IValidationService, ValidationService>();

        // services.AddSingleton<IPublishEndpoint, MockPublishEndpoint>();

        // Bỏ comment phần MassTransit
        services.AddMassTransit(x =>
        {
            x.AddConsumer<UserRequestMessageConsumer>();
            x.AddConsumer<NameLookupConsumer>();
            x.AddConsumer<DepartmentEmployeeConsumer>();
            x.AddConsumer<CompanyEmployeeConsumer>();
            x.AddConsumer<UserEmailConsumer>();

            x.AddConsumer<GetDepartmentUsersConsumer>();
            x.AddConsumer<GetUsersByRoleConsumer>();
            x.AddConsumer<GetUserByIdConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMQ:Host"] ?? "rabbitmq";
                var username = configuration["RabbitMQ:Username"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";

                cfg.Host(host, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                // Thêm logging để debug
                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));

                cfg.ReceiveEndpoint("user-request-queue", e =>
                {
                    e.ConfigureConsumer<UserRequestMessageConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                // Configure endpoint for NameLookup requests
                cfg.ReceiveEndpoint("name-lookup-queue", e =>
                {
                    e.ConfigureConsumer<NameLookupConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                // Configure endpoint for Department Employee requests
                cfg.ReceiveEndpoint("department-employee-queue", e =>
                {
                    e.ConfigureConsumer<DepartmentEmployeeConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                // Configure endpoint for Company Employee requests
                cfg.ReceiveEndpoint("company-employee-queue", e =>
                {
                    e.ConfigureConsumer<CompanyEmployeeConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                // Configure endpoint for User Email requests
                cfg.ReceiveEndpoint("user-email-queue", e =>
                {
                    e.ConfigureConsumer<UserEmailConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });
                cfg.ReceiveEndpoint("get-department-users-queue", e =>
                {
                    e.ConfigureConsumer<GetDepartmentUsersConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                cfg.ReceiveEndpoint("get-users-by-role-queue", e =>
                {
                    e.ConfigureConsumer<GetUsersByRoleConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                cfg.ReceiveEndpoint("get-user-by-id-queue", e =>
                {
                    e.ConfigureConsumer<GetUserByIdConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
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
