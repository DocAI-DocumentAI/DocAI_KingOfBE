using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.Configuration;

public class UserDepartmentConfiguration : IEntityTypeConfiguration<UserDepartment>
{
    public void Configure(EntityTypeBuilder<UserDepartment> builder)
    {
        builder.HasData(
            new UserDepartment
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("595dd357-aaec-455e-9fa7-4fc88d4b819c"), // manager
                DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), // DepartentA
                IsDepartmentHead = true,
            },
            new UserDepartment
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"), // editor
                DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), // DepartentA
                IsDepartmentHead = false,
            },
            new UserDepartment
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("fd05266c-baf5-49bb-a846-554461bcc411"), // member
                DepartmentId = Guid.Parse("86deff8b-cb4b-4daf-88d4-6f366b051836"), // DepartentB
                IsDepartmentHead = false,
            },
            new UserDepartment
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse("fd05266c-baf5-49bb-a846-554461bcc411"), // member
                DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), // DepartentA
                IsDepartmentHead = false,
            }
        );
    }
}