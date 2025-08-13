using System;
using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.Configuration;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasData(
            new Permission {
                Id = Guid.Parse("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),
                Name = "VIEW_ANY_DOCUMENT",
                Description = "Quyền xem mọi tài liệu trong hệ thống ",
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            }
        );
    }
}