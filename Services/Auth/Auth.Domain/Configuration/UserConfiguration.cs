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
                Password = HashPassword("admin"),
                Email = "admin@gmail.com",
                Phone = "0847911068",
                FullName = "Admin",
                RoleId = Guid.Parse("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), //Admin
                DepartmentId = Guid.Parse("d8854d21-8fae-46aa-b51b-0de060b92ee3"), // Company
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            },
            new User
            {
                Id = Guid.Parse("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                Password = HashPassword("manager"),
                Email = "manager@gmail.com",
                Phone = "0123456789",
                FullName = "Manager",
                RoleId = Guid.Parse("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), //Manager
                DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), //DepartmentA
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow
            },
            new User
            {
                Id = Guid.Parse("fd05266c-baf5-49bb-a846-554461bcc411"),
                Password = HashPassword("employee"),
                Email = "employee@gmail.com",
                Phone = "0123456789",
                FullName = "Employee",
                RoleId = Guid.Parse("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), //Employee
                DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), //DepartmentA
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            },
            new User
            {
                Id = Guid.Parse("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                Password = HashPassword("editor"),
                Email = "editor@gmail.com",
                Phone = "0123456789",
                FullName = "Editor",
                RoleId = Guid.Parse("8e7d55e4-67d3-4b73-9995-21b163493136"), //Editor
                DepartmentId = Guid.Parse("8bf13891-1ce9-405c-add9-0ada93308671"), //DepartmentA
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            }
        );
    }
}