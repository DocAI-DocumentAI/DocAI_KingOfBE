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
                Email = "nguyenhuyphc@gmail.com",
                Phone = "0847911068",
                FullName = "Phc Admin",
                Active = true,
                RoleId = Guid.Parse("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), //Admin
                DepartmentId = Guid.Parse("d8854d21-8fae-46aa-b51b-0de060b92ee3"), // Company
                CreatAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            }
        );
    }
}
