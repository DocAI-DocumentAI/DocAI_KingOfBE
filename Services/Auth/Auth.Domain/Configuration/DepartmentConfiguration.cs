using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.Configuration;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.HasData(
            new Department
            {
                Id = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"),
                Name = "DepartentA",
                Description = "DepartentA",
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            },
            new Department
            {
                Id = Guid.Parse("86deff8b-cb4b-4daf-88d4-6f366b051836"),
                Name = "DepartmentB",
                Description = "DepartmentB",
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            }
        );
    }
}