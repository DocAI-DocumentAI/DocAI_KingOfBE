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
            }
        );
    }
}