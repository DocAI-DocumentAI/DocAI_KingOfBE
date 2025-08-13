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
            }
        );
    }
}