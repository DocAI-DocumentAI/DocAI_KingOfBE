using AI.API.Services.Implement;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Implement;
using AI.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;

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

        public static IServiceCollection AddKernelMemoryWithOllama(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind the "Ollama" section from appsettings.json to our strongly-typed class
            var ollamaSettings = configuration.GetSection("Ollama").Get<OllamaConfigSettings>()
                ?? throw new InvalidOperationException("Ollama configuration section is missing in appsettings.json");

            // Create the configuration object that Kernel Memory's native provider expects
            var ollamaConfig = new OllamaConfig
            {
                Endpoint = ollamaSettings.Host,
                TextModel = new OllamaModelConfig(ollamaSettings.ModelName),
                EmbeddingModel = new OllamaModelConfig(ollamaSettings.EmbeddingModelName)
            };

            // Build the Kernel Memory instance using the native Ollama integration
            var memory = new KernelMemoryBuilder(services)
                .WithOllamaTextGeneration(ollamaConfig)
                .WithOllamaTextEmbeddingGeneration(ollamaConfig)
                // For production, you would use a persistent vector store.
                // Example with PostgreSQL/Pgvector. You'd need the Microsoft.KernelMemory.Postgres NuGet package.
                // .WithPostgres("your_postgres_connection_string") 
                .WithSimpleVectorDb()
                .Build();

            services.AddSingleton<IKernelMemory>(memory);
            return services;
        }
        public class OllamaConfigSettings
        {
            public required string Host { get; set; }
            public required string ModelName { get; set; }
            public required string EmbeddingModelName { get; set; }
        }
    }
}
