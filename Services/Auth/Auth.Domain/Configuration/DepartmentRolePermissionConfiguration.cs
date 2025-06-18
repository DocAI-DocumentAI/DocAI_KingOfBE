using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.Configuration;

public class DepartmentRolePermissionConfiguration : IEntityTypeConfiguration<DepartmentRolePermission>
{
    public void Configure(EntityTypeBuilder<DepartmentRolePermission> builder)
    {
        builder.HasData(
            new DepartmentRolePermission
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("595dd357-aaec-455e-9fa7-4fc88d4b819c"), // manager
                DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), // departmentA
                RoleId = Guid.Parse("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), // manager
                PermissionId = Guid.Parse("e72214a0-24bc-471a-aca5-d897f4da0aad"), // VIEW_OWN_DEPARTMENT_DOCUMENT
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow
            },
            new DepartmentRolePermission
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("595dd357-aaec-455e-9fa7-4fc88d4b819c"), // manager
                DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), // departmentA
                RoleId = Guid.Parse("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), // manager
                PermissionId = Guid.Parse("febebe25-dd94-4ba1-bdbd-810e4503bccd"), // VIEW_DEPARTMENT_DOCUMENT
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow
            },
            new DepartmentRolePermission
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"), // editor
                DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), // departmentA
                RoleId = Guid.Parse("8e7d55e4-67d3-4b73-9995-21b163493136"), // editor
                PermissionId = Guid.Parse("febebe25-dd94-4ba1-bdbd-810e4503bccd"), // VIEW_DEPARTMENT_DOCUMENT
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow
            },
            new DepartmentRolePermission
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("fd05266c-baf5-49bb-a846-554461bcc411"), // member
                DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), // departmentA
                RoleId = Guid.Parse("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), // Member
                PermissionId = Guid.Parse("febebe25-dd94-4ba1-bdbd-810e4503bccd"), // VIEW_DEPARTMENT_DOCUMENT
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow
            },
            new DepartmentRolePermission
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("fd05266c-baf5-49bb-a846-554461bcc411"), // member
                DepartmentId = Guid.Parse("86deff8b-cb4b-4daf-88d4-6f366b051836"), // departmentB
                RoleId = Guid.Parse("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), // Member
                PermissionId = Guid.Parse("febebe25-dd94-4ba1-bdbd-810e4503bccd"), // VIEW_DEPARTMENT_DOCUMENT
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow
            }
        );
    }
}