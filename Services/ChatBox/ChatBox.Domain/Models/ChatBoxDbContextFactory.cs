using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChatBox.Domain.Models
{
    internal class ChatBoxDbContextFactory : IDesignTimeDbContextFactory<ChatBoxDbContext>
    {
        public ChatBoxDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            //.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            //.AddEnvironmentVariables()
            .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var optionsBuilder = new DbContextOptionsBuilder<ChatBoxDbContext>();

            optionsBuilder.UseNpgsql(connectionString, builder =>
            {
                builder.MigrationsAssembly(typeof(ChatBoxDbContext).Assembly.GetName().Name);
            });
            return new ChatBoxDbContext(optionsBuilder.Options);
        }
    }
}
