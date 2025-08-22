using Document.Domain.Model;
using Document.Domain.Models;
using Document.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Document.Domain.Context
{
    public class DocAIDocumentContext : DbContext
    {
        public DocAIDocumentContext(DbContextOptions<DocAIDocumentContext> options) : base(options) { }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        public DbSet<DocumentFile> DocumentFiles { get; set; }
        //public DbSet<DocumentChunk> DocumentChunks { get; set; }
        public DbSet<DocumentTag> DocumentTags { get; set; }
        public DbSet<DocumentVersion> DocumentVersions { get; set; }
        public DbSet<DocumentType> DocumentTypes { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ApprovalLog> ApprovalLogs { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }
        public DbSet<ApprovalClaim> ApprovalClaims { get; set; }
        public DbSet<GoogleOAuthToken> GoogleOAuthTokens { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<FolderPermission> FolderPermissions { get; set; }
        public DbSet<AIConfiguration> AIConfigurations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("vector");

            // Apply all configurations from assembly
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApprovalClaim>()
                .HasKey(ac => ac.DocumentVersionId);

            modelBuilder.Entity<ApprovalClaim>()
                .HasOne(ac => ac.DocumentVersion)
                .WithOne()
                .HasForeignKey<ApprovalClaim>(ac => ac.DocumentVersionId);

            // Configure DocumentFile -> DocumentType relationship
            modelBuilder.Entity<DocumentFile>()
                .HasOne(df => df.DocumentType)
                .WithMany(dt => dt.DocumentFiles)
                .HasForeignKey(df => df.DocumentTypeId)
                .IsRequired(true) // Required after data migration
                .OnDelete(DeleteBehavior.Restrict); // Prevent deletion of DocumentType if it has associated documents

            // Configure DocumentFile replacement relationships
            modelBuilder.Entity<DocumentFile>()
                .HasOne(df => df.ReplacementDocument)
                .WithMany()
                .HasForeignKey(df => df.ReplacementId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deletion of replacement document

            modelBuilder.Entity<DocumentFile>()
                .HasOne(df => df.ReplacedByDocument)
                .WithMany()
                .HasForeignKey(df => df.ReplacedById)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deletion of replacing document

            // Configure unique constraint on DocumentType name
            modelBuilder.Entity<DocumentType>()
                .HasIndex(dt => dt.Name)
                .IsUnique();

            // Configure GoogleOAuthToken
            modelBuilder.Entity<GoogleOAuthToken>()
                .HasIndex(t => t.TokenType)
                .IsUnique(); // Ensure only one token per type (company or user)

            // Configure AIConfiguration
            modelBuilder.Entity<AIConfiguration>()
                .HasIndex(ai => ai.IsDefault)
                .HasDatabaseName("IX_AIConfigurations_IsDefault");

            // Note: Folder and FolderPermission configurations are now handled by configuration classes

            //// Configure cascade delete for DocumentChunks
            //modelBuilder.Entity<DocumentFile>()
            //    .HasMany(d => d.DocumentChunks)
            //    .WithOne(c => c.DocumentFile)
            //    .HasForeignKey(c => c.DocumentId)
            //    .OnDelete(DeleteBehavior.Cascade);
        }

    }
}
