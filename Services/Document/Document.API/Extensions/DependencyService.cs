using Document.API.Services.Implements;
using Document.API.Services.Interfaces;
using Document.Domain.Context;
using Document.Infrastructure.Repository.Implement;
using Document.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Document.API.Extensions;

public static class DependencyService
{
    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {
        
        //services.AddScoped<IUnitOfWork<DocAIDocumentContext>, UnitOfWork<DocAIDocumentContext>>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<DocAIDocumentContext>(options =>
            options.UseNpgsql(connectionString, b => b.MigrationsAssembly("Document.API")));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<DbContext, DocAIDocumentContext>();

        return services;
    }

    //public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    //{
    //    var redisConnectionString = configuration.GetConnectionString("Redis");

    //    if (string.IsNullOrEmpty(redisConnectionString))
    //    {
    //        throw new InvalidOperationException(" Connection string cho Redis không được cấu hình.");
    //    }

    //    services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
    //    services.AddScoped<IRedisService, RedisService>();

    //    return services;
    //}

    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDocumentService, DocumentService>();
        return services;
    }
}