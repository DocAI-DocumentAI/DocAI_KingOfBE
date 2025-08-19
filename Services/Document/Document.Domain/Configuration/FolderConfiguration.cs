using Document.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Document.Domain.Configuration
{
    /// <summary>
    /// Entity Framework configuration for Folder entity
    /// </summary>
    public class FolderConfiguration : IEntityTypeConfiguration<Folder>
    {
        public void Configure(EntityTypeBuilder<Folder> builder)
        {
            // Table configuration
            builder.ToTable("Folders");
            builder.HasKey(f => f.Id);

            // Property configurations
            builder.Property(f => f.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(f => f.Description)
                .HasMaxLength(500);

            builder.Property(f => f.DepartmentId)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(f => f.GoogleDriveFolderId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(f => f.FullPath)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(f => f.FolderType)
                .HasConversion<int>(); // Store enum as integer

            // Indexes
            builder.HasIndex(f => new { f.ParentFolderId, f.Name })
                .IsUnique()
                .HasDatabaseName("IX_Folders_ParentId_Name");

            builder.HasIndex(f => f.GoogleDriveFolderId)
                .IsUnique()
                .HasDatabaseName("IX_Folders_GoogleDriveId");

            builder.HasIndex(f => f.FullPath)
                .IsUnique()
                .HasDatabaseName("IX_Folders_FullPath");

            builder.HasIndex(f => f.DepartmentId)
                .HasDatabaseName("IX_Folders_DepartmentId");

            builder.HasIndex(f => f.IsSystemFolder)
                .HasDatabaseName("IX_Folders_IsSystemFolder");

            builder.HasIndex(f => f.IsPublic)
                .HasDatabaseName("IX_Folders_IsPublic");

            builder.HasIndex(f => f.IsDeleted)
                .HasDatabaseName("IX_Folders_IsDeleted");

            // Self-referencing relationship
            builder.HasOne(f => f.ParentFolder)
                .WithMany(f => f.SubFolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationships with other entities
            builder.HasMany(f => f.Documents)
                .WithOne(dv => dv.Folder)
                .HasForeignKey(dv => dv.FolderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(f => f.FolderPermissions)
                .WithOne(fp => fp.Folder)
                .HasForeignKey(fp => fp.FolderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Seed data for system folders will be handled by migration
            // as it depends on department data from Auth service
        }
    }
}
