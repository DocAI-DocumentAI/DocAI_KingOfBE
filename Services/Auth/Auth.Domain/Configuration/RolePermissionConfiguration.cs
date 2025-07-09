using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.Configuration;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasData(
            new RolePermission
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.Parse("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),// VIEW_ANY_DOCUMENT
                RoleId = Guid.Parse("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), // Admin
            },
            new RolePermission
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.Parse("e72214a0-24bc-471a-aca5-d897f4da0aad"), // VIEW_OWN_DEPARTMENT_DOCUMENT
                RoleId = Guid.Parse("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), // Manager
            },
            new RolePermission
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.Parse("febebe25-dd94-4ba1-bdbd-810e4503bccd"), // VIEW_DEPARTMENT_DOCUMENT
                RoleId = Guid.Parse("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), // Employee
            },
            new RolePermission
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.Parse("febebe25-dd94-4ba1-bdbd-810e4503bccd"), // VIEW_DEPARTMENT_DOCUMENT
                RoleId = Guid.Parse("8e7d55e4-67d3-4b73-9995-21b163493136"), // Editor
            },
            new RolePermission
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.Parse("febebe25-dd94-4ba1-bdbd-810e4503bccd"), // VIEW_DEPARTMENT_DOCUMENT
                RoleId = Guid.Parse("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), // Manager
            }
        );
    }
}