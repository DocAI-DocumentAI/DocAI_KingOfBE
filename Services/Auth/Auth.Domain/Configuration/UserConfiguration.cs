using System.Security.Cryptography;
using System.Text;
using Auth.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Domain.Configuration;

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
        builder.HasData(
            new User
            {
                Id = Guid.Parse("13d466ed-8a2d-414d-88c0-9c7adcac2616"),
                UserName = "admin",
                Password = HashPassword("admin"),
                Email = "admin@gmail.com",
                Phone = "0847911068",
                FullName = "Admin",
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
                TwoFactorEnabled = false,
                TwoFactorMethod = "Email"
            },
            new User
            {
                Id = Guid.Parse("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                UserName = "manager",
                Password = HashPassword("manager"),
                Email = "manager@gmail.com",
                Phone = "0123456789",
                FullName = "Manager",
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
                TwoFactorEnabled = false,
                TwoFactorMethod = "Email"
            },
            new User
            {
                Id = Guid.Parse("fd05266c-baf5-49bb-a846-554461bcc411"),
                UserName = "member",
                Password = HashPassword("member"),
                Email = "member@gmail.com",
                Phone = "0123456789",
                FullName = "Member",
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
                TwoFactorEnabled = false,
                TwoFactorMethod = "Email"
            },
            new User
            {
                Id = Guid.Parse("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                UserName = "editor",
                Password = HashPassword("editor"),
                Email = "editor@gmail.com",
                Phone = "0123456789",
                FullName = "Member1",
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
                TwoFactorEnabled = false,
                TwoFactorMethod = "Email"
            }
        );
    }
}