using AI.API.Services.Implement;
using AI.API.Services.Interface;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Implement;
using AI.Infrastructure.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.TextGeneration;
using Polly;
using Microsoft.Extensions.AI;

namespace AI.API.Extensions
{
    public static class DependencyService
    {
        public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork<DocAIDbContext>, UnitOfWork<DocAIDbContext>>();
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
            var kernelBuilder = Kernel.CreateBuilder();

            string textModel = configuration["HuggingFace:TextModel"];
            string textEndpoint = configuration["HuggingFace:TextEndpoint"];
            string embeddingModel = configuration["HuggingFace:EmbeddingModel"];
            string embeddingEndpoint = configuration["HuggingFace:EmbeddingEndpoint"];
            string apiKey = configuration["HuggingFace:ApiKey"];

            kernelBuilder.Services.AddHuggingFaceTextGeneration(
                  //model: textModel,
                  endpoint: new Uri(textEndpoint),
                  apiKey: apiKey,
                  serviceId: "hf-text-gen",
                      httpClient: new HttpClient(new LoggingHandler(new HttpClientHandler()))

              );    

            kernelBuilder.Services.AddHuggingFaceEmbeddingGenerator(
                model: embeddingModel,
                endpoint: new Uri(embeddingEndpoint),
                apiKey: apiKey,
                serviceId: "hf-embed-gen"
            );
            services.AddSingleton(kernelBuilder.Build());

            services.AddScoped<IAIService, AIService>();

            return services;
        }
    }
}
