using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ChatBox.Domain.Models
{
    public class ChatBoxDbContext : DbContext
    {
        public ChatBoxDbContext(DbContextOptions<ChatBoxDbContext> options) : base(options) { }

        public DbSet<Conversation> Conversations { get; set; } = null!;
        public DbSet<MessageHistory> MessageHistories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Conversation>()
                .HasMany(c => c.MessageHistories)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade); 

            modelBuilder.Entity<MessageHistory>()
                .HasIndex(m => new { m.ConversationId, m.Order });

        }
    }
}
