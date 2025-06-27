using AI.API.Services.Implement;
using Microsoft.KernelMemory.AI;

namespace AI.API.Extensions
{
    public static class ApplicationServiceExtensions
    {

        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // REVIEW POINT: Đăng ký Text Tokenizer của Kernel Memory (Tiktoken)
            services.AddSingleton<ITextTokenizer, Services.Implement.CL100KTokenizer>();

            // REVIEW POINT: Đăng ký các triển khai của ITextGenerator và ITextEmbeddingGenerator cho Kernel Memory
            services.AddScoped<ITextGenerator, OllamaTextGeneratorKm>(); // Triển khai ITextGenerator dùng OllamaSharp
            services.AddScoped<ITextEmbeddingGenerator, OllamaTextEmbeddingGeneratorKm>(); // Triển khai ITextEmbeddingGenerator dùng OllamaSharp

            // Gọi các extension cho Database và Unit of Work của bạn (nếu có)
            services.AddDatabase();
            services.AddUnitOfWork();

            return services;
        }
    }
}
