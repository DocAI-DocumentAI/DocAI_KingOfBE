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
        public DbSet<SessionPreference> SessionPreferences { get; set; }
        public DbSet<ProhibitedWord> ProhibitedWords { get; set; }
        public DbSet<AIConfiguration> AIConfigurations { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChatSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).HasMaxLength(500);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.ModelName).HasMaxLength(100);
            });

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.HasOne(d => d.Session)
                    .WithMany(p => p.Messages)
                    .HasForeignKey(d => d.SessionId);
            });

            modelBuilder.Entity<SessionPreference>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Key).HasMaxLength(100);
                entity.HasOne(d => d.Session)
                    .WithMany(p => p.Preferences)
                    .HasForeignKey(d => d.SessionId);
            });

            modelBuilder.Entity<ProhibitedWord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Word).IsRequired().HasMaxLength(200);
                entity.HasIndex(e => e.Word).IsUnique();
            });

            modelBuilder.Entity<AIConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Provider).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ModelName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ApiKey).IsRequired();
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}
