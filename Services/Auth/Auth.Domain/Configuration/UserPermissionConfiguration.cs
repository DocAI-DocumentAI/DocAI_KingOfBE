using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.Configuration;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.HasData(
            new UserPermission
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.Parse("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),// VIEW_ANY_DOCUMENT
                UserId = Guid.Parse("13d466ed-8a2d-414d-88c0-9c7adcac2616"), // Admin
            },
            new UserPermission
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.Parse("e72214a0-24bc-471a-aca5-d897f4da0aad"), // VIEW_OWN_DEPARTMENT_DOCUMENT
                UserId = Guid.Parse("595dd357-aaec-455e-9fa7-4fc88d4b819c"), // Manager
            },
            new UserPermission
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.Parse("febebe25-dd94-4ba1-bdbd-810e4503bccd"), // VIEW_DEPARTMENT_DOCUMENT
                UserId = Guid.Parse("fd05266c-baf5-49bb-a846-554461bcc411"), // Employee
            },
            new UserPermission
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.Parse("febebe25-dd94-4ba1-bdbd-810e4503bccd"), // VIEW_DEPARTMENT_DOCUMENT
                UserId = Guid.Parse("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"), // Editor
            },
            new UserPermission
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.Parse("febebe25-dd94-4ba1-bdbd-810e4503bccd"), // VIEW_DEPARTMENT_DOCUMENT
                UserId = Guid.Parse("595dd357-aaec-455e-9fa7-4fc88d4b819c"), // Manager
            }
        );
    }
}