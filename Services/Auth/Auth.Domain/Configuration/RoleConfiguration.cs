using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Auth.Domain.Models;

namespace Auth.Domain.Configuration;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasData(
            new Role
            {
                Id = Guid.Parse("4e29a870-9131-4cc2-97ca-eaa748b5f17f"),
                RoleName = "Member",
                Description = "Member",
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            },
            new Role
            {
                Id = Guid.Parse("a996692c-1f5e-4458-8dcf-c2494a47b6d6"),
                RoleName = "Admin",
                Description = "Admin",
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            },
            new Role
            {
                Id = Guid.Parse("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"),
                RoleName = "Manager",
                Description = "Manager",
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            },
            new Role
            {
                Id = Guid.Parse("8e7d55e4-67d3-4b73-9995-21b163493136"),
                RoleName = "Editor",
                Description = "Editor",
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            }
        );
    }
}