using Document.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Document.Domain.Configuration
{
    /// <summary>
    /// Entity Framework configuration for FolderPermission entity
    /// </summary>
    public class FolderPermissionConfiguration : IEntityTypeConfiguration<FolderPermission>
    {
        public void Configure(EntityTypeBuilder<FolderPermission> builder)
        {
            // Table configuration
            builder.ToTable("FolderPermissions");
            builder.HasKey(fp => fp.Id);

            // Property configurations
            builder.Property(fp => fp.FolderId)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(fp => fp.UserId)
                .HasMaxLength(50);

            builder.Property(fp => fp.DepartmentId)
                .HasMaxLength(50);

            builder.Property(fp => fp.PermissionType)
                .IsRequired()
                .HasConversion<int>(); // Store enum as integer

            builder.Property(fp => fp.ParentPermissionId)
                .HasMaxLength(50);

            // Indexes
            builder.HasIndex(fp => new { fp.FolderId, fp.UserId, fp.DepartmentId })
                .IsUnique()
                .HasDatabaseName("IX_FolderPermissions_Folder_User_Department");

            builder.HasIndex(fp => fp.FolderId)
                .HasDatabaseName("IX_FolderPermissions_FolderId");

            builder.HasIndex(fp => fp.UserId)
                .HasDatabaseName("IX_FolderPermissions_UserId");

            builder.HasIndex(fp => fp.DepartmentId)
                .HasDatabaseName("IX_FolderPermissions_DepartmentId");

            builder.HasIndex(fp => fp.IsInherited)
                .HasDatabaseName("IX_FolderPermissions_IsInherited");

            builder.HasIndex(fp => fp.IsActive)
                .HasDatabaseName("IX_FolderPermissions_IsActive");

            builder.HasIndex(fp => fp.ExpiresAt)
                .HasDatabaseName("IX_FolderPermissions_ExpiresAt");

            // Relationships
            builder.HasOne(fp => fp.Folder)
                .WithMany(f => f.FolderPermissions)
                .HasForeignKey(fp => fp.FolderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Self-referencing relationship for permission inheritance
            builder.HasOne(fp => fp.ParentPermission)
                .WithMany(fp => fp.ChildPermissions)
                .HasForeignKey(fp => fp.ParentPermissionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Check constraints
            builder.HasCheckConstraint("CK_FolderPermissions_UserOrDepartment",
                "\"UserId\" IS NOT NULL OR \"DepartmentId\" IS NOT NULL");

            builder.HasCheckConstraint("CK_FolderPermissions_NotBothUserAndDepartment",
                "NOT (\"UserId\" IS NOT NULL AND \"DepartmentId\" IS NOT NULL)");
        }
    }
}
