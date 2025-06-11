using System;
using System.Security.Cryptography;
using System.Text;
using Auth.Domain.Enums;
using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.ConversationConfiguration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    public static string HashPassword(string password)
    {
        byte[] salt;
        using (var rng = RandomNumberGenerator.Create())
        {
            salt = new byte[SaltSize];
            rng.GetBytes(salt);
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256);

        byte[] hash = pbkdf2.GetBytes(HashSize);

        byte[] hashBytes = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, hashBytes, 0, SaltSize);
        Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

        return Convert.ToBase64String(hashBytes);
    }
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(r => r.Role)
            .HasConversion(
                v => v.ToString(),
                v => (RoleEnum)Enum.Parse(typeof(RoleEnum), v)
            );
        var adminUsername = Environment.GetEnvironmentVariable("ADMIN_USERNAME");
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        builder.HasData(new User()
        {
            UserId = Guid.NewGuid(),
            UserName = "${adminUsername}",
            Password = HashPassword("${adminPassword}"),
            Email = "admin@gmail.com",
            Phone = "0847911068",
            FullName = "Admin",
            Role = RoleEnum.Admin,
            CreatAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow, 
            TwoFactorEnabled = false, 
            TwoFactorMethod = "Email" 
        });
    }
}