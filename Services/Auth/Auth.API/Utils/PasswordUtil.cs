using System;
using System.Security.Cryptography;
using System.Text;

namespace Auth.API.Utils;

public class PasswordUtil
{
    // Độ dài salt (byte)
    private const int SaltSize = 16;
    // Độ dài hash (byte)
    private const int HashSize = 32;
    // Số lần lặp (iteration) - càng cao càng an toàn, nhưng chậm hơn
    private const int Iterations = 100_000;

    public static string HashPassword(string password)
    {
        // Tạo salt ngẫu nhiên
        byte[] salt;
        using (var rng = RandomNumberGenerator.Create())
        {
            salt = new byte[SaltSize];
            rng.GetBytes(salt);
        }

        // Hash mật khẩu với PBKDF2
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256);

        byte[] hash = pbkdf2.GetBytes(HashSize);

        // Kết hợp salt và hash, lưu dưới dạng Base64
        byte[] hashBytes = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, hashBytes, 0, SaltSize);
        Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

        return Convert.ToBase64String(hashBytes);
    }

    public static bool VerifyPassword(string password, string hashedPassword)
    {
        // Giải mã hashedPassword từ Base64
        byte[] hashBytes = Convert.FromBase64String(hashedPassword);

        // Kiểm tra độ dài hợp lệ
        if (hashBytes.Length != SaltSize + HashSize)
            return false;

        // Tách salt và hash
        byte[] salt = new byte[SaltSize];
        byte[] storedHash = new byte[HashSize];
        Array.Copy(hashBytes, 0, salt, 0, SaltSize);
        Array.Copy(hashBytes, SaltSize, storedHash, 0, HashSize);

        // Tạo hash từ mật khẩu người dùng nhập
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256);

        byte[] computedHash = pbkdf2.GetBytes(HashSize);

        // So sánh hash
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }
    // public static string HashPassword(string rawPassword)
    // {
    //     using var sha256 = SHA256.Create();
    //     var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawPassword));
    //     return Convert.ToBase64String(bytes);
    // }
}