using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Auth.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Auth.API.Utils;

public static class JwtUtil
{
    public static string GenerateJwtToken(User user, IConfiguration configuration)
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

        // Lấy danh sách permissions từ user.UserPermissions
        var permissions = user.UserPermissions?
            .Select(up => up.Permission.Name)
            .ToList() ?? new List<string>();

        // Chuyển danh sách permissions thành chuỗi phân cách bởi dấu phẩy
        string permissionsString = string.Join(",", permissions);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("userId", user.Id.ToString()),
            new Claim("email", user.Email ?? ""),
            new Claim("fullName", user.FullName ?? ""),
            new Claim("phone", user.Phone ?? ""),
            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? ""),
            new Claim("departmentId", user.Department?.Id.ToString() ?? ""),
            new Claim("departmentName", user.Department?.Name ?? ""),
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

    public static string GenerateJwtRefreshToken(User user, IConfiguration configuration)
    {
        string secret = configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret is missing in configuration.");
        string issuer = configuration["JWT:Issuer"] ?? throw new InvalidOperationException("JWT:Issuer is missing in configuration.");

        if (secret.Length < 32)
        {
            throw new InvalidOperationException("JWT:Secret must be at least 32 characters long for HS256.");
        }

        var key = Encoding.UTF8.GetBytes(secret);
        var tokenHandler = new JwtSecurityTokenHandler();

        var permissions = user.UserPermissions?
            .Select(up => up.Permission.Name)
            .ToList() ?? new List<string>();

        string permissionsString = string.Join(",", permissions);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("userId", user.Id.ToString()),
                new Claim("email", user.Email ?? ""),
                new Claim("fullName", user.FullName ?? ""),
                new Claim("phone", user.Phone ?? ""),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? ""),
                new Claim("departmentId", user.Department?.Id.ToString() ?? ""),
                new Claim("departmentName", user.Department?.Name ?? ""),
                new Claim("departmentID", user.Department?.Id.ToString() ?? ""),
                new Claim("permissions", permissionsString)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = issuer,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public static ClaimsPrincipal? ValidateRefreshToken(string refreshToken, IConfiguration configuration)
    {
        try
        {
            string secret = configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret is missing in configuration.");
            string issuer = configuration["JWT:Issuer"] ?? throw new InvalidOperationException("JWT:Issuer is missing in configuration.");

            var key = Encoding.UTF8.GetBytes(secret);
            var tokenHandler = new JwtSecurityTokenHandler();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(refreshToken, validationParameters, out _);

            // Verify it's a refresh token
            var tokenType = principal.FindFirst("tokenType")?.Value;
            if (tokenType != "refresh")
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public static string ExtractJtiFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            return jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;
        }
        catch
        {
            return null;
        }
    }

    public static DateTime? GetTokenExpiration(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            var exp = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp)?.Value;

            if (long.TryParse(exp, out var expUnix))
            {
                return DateTimeOffset.FromUnixTimeSeconds(expUnix).DateTime;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, IConfiguration configuration)
    {
        try
        {
            string secret = configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret is missing in configuration.");
            string issuer = configuration["JWT:Issuer"] ?? throw new InvalidOperationException("JWT:Issuer is missing in configuration.");

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = false,
                ValidateLifetime = false, // Don't validate lifetime for expired tokens
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
            return principal;
        }
        catch
        {
            return null;
        }
    }
}
