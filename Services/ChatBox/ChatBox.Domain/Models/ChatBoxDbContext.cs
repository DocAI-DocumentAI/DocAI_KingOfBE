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

        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<AIConfiguration> AIConfigurations { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChatSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.ModelName).IsRequired().HasMaxLength(200);
                entity.HasIndex(e => new { e.UserId, e.IsActive });
            });

            // ChatMessage  
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SessionId).IsRequired();
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Role).IsRequired();

                entity.HasOne(e => e.Session)
                    .WithMany(s => s.Messages)
                    .HasForeignKey(e => e.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.SessionId, e.CreatedAt });
            });

            modelBuilder.Entity<AIConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ModelName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Temperature).HasPrecision(3, 2);
                entity.Property(e => e.TopP).HasPrecision(3, 2);
                entity.HasIndex(e => e.ModelName).IsUnique();
                entity.HasIndex(e => e.IsActive);
            });

            modelBuilder.Entity<UserPreference>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.SessionId).HasMaxLength(100);
                entity.Property(e => e.UserName).HasMaxLength(100);

                entity.HasOne(e => e.Session)
                    .WithMany(s => s.Preferences)
                    .HasForeignKey(e => e.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.SessionId }).IsUnique();
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}
