using Document.Domain.Model;
using Document.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ApprovalLog> ApprovalLogs { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }
        public DbSet<ApprovalClaim> ApprovalClaims { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("vector");
            base.OnModelCreating(modelBuilder);

            //// Configure cascade delete for DocumentChunks
            //modelBuilder.Entity<DocumentFile>()
            //    .HasMany(d => d.DocumentChunks)
            //    .WithOne(c => c.DocumentFile)
            //    .HasForeignKey(c => c.DocumentId)
            //    .OnDelete(DeleteBehavior.Cascade);
        }

    }
}
