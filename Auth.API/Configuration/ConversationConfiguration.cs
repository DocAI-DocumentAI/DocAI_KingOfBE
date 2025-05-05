using System;
using System.Security.Cryptography;
using System.Text;
using Auth.API.Enums;
using Auth.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.API.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(r => r.Role)
            .HasConversion(
                v => v.ToString(),
                v => (RoleEnum)Enum.Parse(typeof(RoleEnum), v)
            );
        builder.HasData(new User()
        {
            UserId = Guid.NewGuid(),
            UserName = "admin",
            Password = Convert.ToBase64String(new SHA256Managed().ComputeHash(Encoding.UTF8.GetBytes("admin"))),
            Email = "admin@gmail.com",
            Phone = "0847911068",
            FullName = "Admin",
            Role = RoleEnum.Manager,
            CreatAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow, 
            TwoFactorEnabled = false, 
            TwoFactorMethod = "Email" 
        });
    }
}