using ChatBox.API.Mappers;
using ChatBox.API.Services.Implement;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Implement;
using ChatBox.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChatBox.API.Extensions;

public static class DependencyService
{
    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {
        //
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
        services.AddDatabase(); // Sử dụng tên AddChatDatabase

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork<ChatBoxDbContext>, UnitOfWork<ChatBoxDbContext>>();

        services.AddHttpClient<IAIClient, AIClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["ChatService:AIMicroserviceBaseUrl"] ?? throw new InvalidOperationException("AI Microservice Base URL is missing."));
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        services.AddHttpClient<IDocumentClient, DocumentClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["ChatService:DocumentMicroserviceBaseUrl"] ?? throw new InvalidOperationException("Document Microservice Base URL is missing."));
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        services.AddScoped<IChatService, ChatService>();
        services.AddAutoMapper(typeof(MappingProfile).Assembly);
        return services;
    }
    //public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    //{
    //    services.AddMassTransit(x =>
    //    {
    //        x.AddConsumer<UserRequestMessageConsumer>(); 
    //        x.UsingRabbitMq((context, cfg) =>
    //        {
    //            cfg.Host("rabbitmq://localhost", h =>
    //            {
    //                h.Username("guest");
    //                h.Password("guest");
    //            });
                
    //            cfg.ReceiveEndpoint("user-request-queue", e =>
    //            {
    //                // Chỉ định consumer nào sẽ xử lý message từ queue này
    //                e.ConfigureConsumer<UserRequestMessageConsumer>(context);
    //            });
    //        });
    //    });
    //    return services;
    //}
}