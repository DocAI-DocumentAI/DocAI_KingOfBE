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
        List<ContextualPermissionClaim> contextualPermissions,
        IConfiguration configuration)
    {
        string secret = configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret is missing in configuration.");
        string issuer = configuration["JWT:Issuer"] ?? throw new InvalidOperationException("JWT:Issuer is missing in configuration.");
        string audience = configuration["JWT:Audience"] ?? ""; // Lấy Audience từ config

        if (secret.Length < 32)
        {
            throw new InvalidOperationException("JWT:Secret must be at least 32 characters long for HS256.");
        }

        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName ?? ""),
            new Claim("userId", user.Id.ToString()),
            new Claim("email", user.Email ?? ""),
            new Claim("fullName", user.FullName ?? "") // Thêm FullName vào claim
        };

        // 1. Thêm Contextual Permissions (dưới dạng claim tùy chỉnh "contextualPermissions")
        if (contextualPermissions != null && contextualPermissions.Any())
        {
            claims.Add(new Claim(
                "contextualPermissions",
                JsonConvert.SerializeObject(contextualPermissions),
                JsonClaimValueTypes.JsonArray // Quan trọng để chỉ định đây là JSON array
            ));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1), // Thời gian hết hạn
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