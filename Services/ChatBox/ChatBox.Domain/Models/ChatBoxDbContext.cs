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

        // ✅ Core chat entities
        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<UserPreference> UserChatPreferences { get; set; }

        // ✅ ADD MISSING DbSets for your services to work
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SecurityAuditLog> SecurityAuditLogs { get; set; }
        public DbSet<ContentModerationRule> ContentModerationRules { get; set; }
        public DbSet<ModerationLog> ModerationLogs { get; set; }
        public DbSet<UserModerationHistory> UserModerationHistory { get; set; }
        public DbSet<RateLimitRule> RateLimitRules { get; set; }
        public DbSet<UserRateLimitStatus> UserRateLimitStatuses { get; set; }
        public DbSet<RateLimitViolation> RateLimitViolations { get; set; }
        public DbSet<SecurityIncident> SecurityIncidents { get; set; }
        public DbSet<UserSecurityProfile> UserSecurityProfiles { get; set; }
        public DbSet<MessageFeedback> MessageFeedbacks { get; set; }
        public DbSet<SystemPreference> SystemPreferences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
