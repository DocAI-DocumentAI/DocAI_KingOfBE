using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Auth.API.Payload;
using Auth.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace Auth.API.Utils;

public class JwtUtil
{
    public JwtUtil()
    {

    }
    public static string GenerateJwtToken(
        User user,
        IConfiguration configuration)
    {
        string secret = configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret is missing in configuration.");
        string issuer = configuration["JWT:Issuer"] ?? throw new InvalidOperationException("JWT:Issuer is missing in configuration.");
        string audience = configuration["JWT:Audience"] ?? "";

        if (secret.Length < 32)
        {
            throw new InvalidOperationException("JWT:Secret must be at least 32 characters long for HS256.");
        }

        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

        // Lấy danh sách permissions từ user.Role.RolePermissions
        var permissions = user.Role.RolePermissions
            .Select(rp => rp.Permission.Name)
            .ToList();

        // Chuyển danh sách permissions thành chuỗi phân cách bởi dấu phẩy
        string permissionsString = string.Join(",", permissions);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName ?? ""),
            new Claim("userId", user.Id.ToString()),
            new Claim("email", user.Email ?? ""),
            new Claim("fullName", user.FullName ?? ""),
            new Claim("phone", user.Phone ?? ""),
            new Claim(ClaimTypes.Role, user.Role.RoleName ?? ""),
            new Claim("departmentName", user.Department.Name ?? ""),
            new Claim("permissions", permissionsString)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }


    public static string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }

}
