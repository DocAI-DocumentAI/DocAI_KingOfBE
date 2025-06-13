using AI.API.Services.Implement;
using AI.API.Services.Interface;

namespace AI.API.Extensions
{
    public static class ApplicationServiceExtensions
    {

        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // IConfiguration sẽ được tự động inject vào các service cần dùng.
            services.AddScoped<IOllamaAIService, OllamaAIService>();

            // Gọi các extension từ Infrastructure (nếu cần)
            // AddDatabase và AddUnitOfWork cần IConfiguration
            services.AddDatabase();
            services.AddUnitOfWork();

            return services;
        }
    }
}
