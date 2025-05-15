using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Document.Domain.Context
{
    public class DocAIDocumentContextFactory : IDesignTimeDbContextFactory<DocAIDocumentContext>
    {
        public DocAIDocumentContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // Thư mục hiện tại
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<DocAIDocumentContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            optionsBuilder.UseNpgsql(connectionString);

            return new DocAIDocumentContext(optionsBuilder.Options);
        }
    }
}
