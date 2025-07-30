using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notification.API.Consumers;
using Notification.API.Jobs;
using Notification.API.Services.Implement;
using Notification.API.Services.Interfaces;
using Notification.API.Utils;
using Notification.Domain.Models;
using Notification.Infrastructure.Repository.Implement;
using Notification.Infrastructure.Repository.Interfaces;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using Quartz;
using Serilog;

namespace Auth.API.Extensions;

public static class DependencyService
{
    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {
         services.AddScoped<IUnitOfWork<NotificationDbContext>, UnitOfWork<NotificationDbContext>>();
        return services;
    }
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

         services.AddDbContext<NotificationDbContext>(options =>
             options.UseNpgsql(connectionString, b => b.MigrationsAssembly("Notification.API")));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        // services.AddScoped<DbContext, DocAIAuthContext>();

        return services;
    }
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<INotificationConfigService, NotificationConfigService>();
        services.AddScoped<INotificationLogService, NotificationLogService>();
        services.AddScoped<INotificationSchedulerService, NotificationSchedulerService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDocumentScanService, DocumentScanService>();

        services.AddSingleton<TemplateRendererUtil>();
        services.AddMemoryCache(); 

        return services;
    }
    public static IServiceCollection AddApiClients(this IServiceCollection services, IConfiguration configuration)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

        // Client cho Auth Microservice
        services.AddHttpClient<IAuthClient, AuthClient>(client =>
            client.BaseAddress = new Uri(configuration["MicroserviceUrls:Auth"]!))
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(circuitBreakerPolicy);

                    // Client cho Document Microservice (sử dụng Mock trong môi trường DEBUG)
            #if DEBUG
                    services.AddScoped<IDocumentClient, MockDocumentClient>();
            #else
                        services.AddHttpClient<IDocumentClient, DocumentClient>(client => 
                            client.BaseAddress = new Uri(configuration["MicroserviceUrls:Document"]!))
                            .AddPolicyHandler(retryPolicy)
                            .AddPolicyHandler(circuitBreakerPolicy);
            #endif

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
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.Request.Path.StartsWithSegments("/notificationHub"))
                        {
                            context.Token = accessToken;
                        }
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
    public static IServiceCollection AddQuartzJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();

            // NotificationScanJob
            var scanJobKey = new JobKey("NotificationScanJob");
            q.AddJob<NotificationScanJob>(opts => opts.WithIdentity(scanJobKey));
            q.AddTrigger(opts => opts
                .ForJob(scanJobKey)
                .WithIdentity("NotificationScanTrigger")
                .WithCronSchedule(configuration["Quartz:ScanCronExpression"] ?? "0 0 7 * * ?")); // 7:00 AM daily

            // CleanUpOldLogsJob
            var cleanupJobKey = new JobKey("CleanUpOldLogsJob");
            q.AddJob<CleanUpOldLogsJob>(opts => opts.WithIdentity(cleanupJobKey));
            q.AddTrigger(opts => opts
                .ForJob(cleanupJobKey)
                .WithIdentity("CleanUpOldLogsTrigger")
                .WithCronSchedule(configuration["Quartz:CleanupCronExpression"] ?? "0 0 0 ? * SUN")); // Sunday midnight
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        return services;
    }
    public static IServiceCollection AddMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(busConfig =>
        {
            busConfig.AddConsumer<ProcessDocumentExpirationConsumer>(); // Sẽ được tích hợp sâu hơn ở bước sau
            busConfig.AddConsumer<GeneralNotificationConsumer>();

            busConfig.UsingRabbitMq((context, mqConfig) =>
            {
                //var host = configuration["RabbitMQ:Host"] ?? "rabbitmq";
                var username = configuration["RabbitMQ:Username"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";

                mqConfig.Host("localhost", "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });
                mqConfig.UseRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));

                mqConfig.ReceiveEndpoint("document-expiration-queue", e => 
                {
                    e.ConfigureConsumer<ProcessDocumentExpirationConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });

                mqConfig.ReceiveEndpoint("general-notifications-queue", e => 
                {
                    e.ConfigureConsumer<GeneralNotificationConsumer>(context);
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.UseInMemoryOutbox();
                });
            });
        });
        return services;
    }

}
