using ChatBox.API.Consumers;
using MassTransit;

namespace ChatBox.API.Extensions;

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
        return services;
    }
}