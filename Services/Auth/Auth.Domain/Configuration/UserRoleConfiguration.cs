using System;
using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.Configuration;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasData(
            new UserRole
            {
                Id = Guid.NewGuid(),
                RoleId = Guid.Parse("8e7d55e4-67d3-4b73-9995-21b163493136"), // Editor
                UserId = Guid.Parse("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"), // editor
            },
            new UserRole
            {
                Id = Guid.NewGuid(),
                RoleId = Guid.Parse("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), // Manager
                UserId = Guid.Parse("595dd357-aaec-455e-9fa7-4fc88d4b819c"), // manager
            },
            new UserRole
            {
                Id = Guid.NewGuid(),
                RoleId = Guid.Parse("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), // Admin
                UserId = Guid.Parse("13d466ed-8a2d-414d-88c0-9c7adcac2616"), // admin
            },
            new UserRole
            {
                Id = Guid.NewGuid(),
                RoleId = Guid.Parse("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), // Member
                UserId = Guid.Parse("fd05266c-baf5-49bb-a846-554461bcc411"), // member
            }
        );
    }
}