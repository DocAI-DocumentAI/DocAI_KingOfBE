using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Auth.API.Models
{
    public class DocAIAuthContextFactory : IDesignTimeDbContextFactory<DocAIAuthContext>
    {
        public DocAIAuthContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // Thư mục hiện tại
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<DocAIAuthContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            optionsBuilder.UseNpgsql(connectionString);

            return new DocAIAuthContext(optionsBuilder.Options);
        }
    }
}
