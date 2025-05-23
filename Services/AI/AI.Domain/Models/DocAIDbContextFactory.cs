using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AI.Domain.Models
{
    public class DocAIDbContextFactory : IDesignTimeDbContextFactory<DocAIDbContext>
    {
        public DocAIDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            //.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            //.AddEnvironmentVariables()
            .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var optionsBuilder = new DbContextOptionsBuilder<DocAIDbContext>();

            optionsBuilder.UseNpgsql(connectionString, builder =>
            {
                builder.MigrationsAssembly(typeof(DocAIDbContext).Assembly.GetName().Name);
            });
            return new DocAIDbContext(optionsBuilder.Options);
        }
    }
}
