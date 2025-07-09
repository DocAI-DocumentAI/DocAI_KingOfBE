using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.Configuration;

public class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
{
    public void Configure(EntityTypeBuilder<UserSetting> builder)
    {
        builder.HasData(
            new UserSetting
            {
                Id = Guid.Parse("ddfcbea3-56e9-4187-97f6-521ca24c2412"),
                TwoFactorEnabled = false,
                TwoFactorMethod = "email",
                NotificationsEnabled = true,
                UpdateAt = DateTime.UtcNow,
                UserId = Guid.Parse("13d466ed-8a2d-414d-88c0-9c7adcac2616") //Admin
            },
            new UserSetting
            {
                Id = Guid.Parse("dd9105eb-4df0-4c32-bc55-fd0169e386fc"),
                TwoFactorEnabled = false,
                TwoFactorMethod = "email",
                NotificationsEnabled = true,
                UpdateAt = DateTime.UtcNow,
                UserId = Guid.Parse("595dd357-aaec-455e-9fa7-4fc88d4b819c") //Manager
            },
            new UserSetting
            {
                Id = Guid.Parse("86254802-1d1e-4734-a25b-ef22ff39cefc"),
                TwoFactorEnabled = false,
                TwoFactorMethod = "email",
                NotificationsEnabled = true,
                UpdateAt = DateTime.UtcNow,
                UserId = Guid.Parse("fd05266c-baf5-49bb-a846-554461bcc411") //Employee
            },
            new UserSetting
            {
                Id = Guid.Parse("4e8bff21-b470-4b9e-92da-400d21992f96"),
                TwoFactorEnabled = false,
                TwoFactorMethod = "email",
                NotificationsEnabled = true,
                UpdateAt = DateTime.UtcNow,
                UserId = Guid.Parse("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") //Editor
            }
        );
    }
}