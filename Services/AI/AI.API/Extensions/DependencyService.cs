using AI.API.Services.Implement;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Implement;
using AI.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AI.API.Extensions
{
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

            services.AddDbContext<DocAIDbContext>(options =>
                    options.UseNpgsql(connectionString, builder =>
                    {
                        builder.MigrationsAssembly(typeof(DocAIDbContext).Assembly.GetName().Name);
                    }));

            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<DbContext, DocAIDbContext>();

            return services;
        }
        public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
        {

            return services;
        }
    }
}
