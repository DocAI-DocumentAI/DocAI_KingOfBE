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
public class JwtUtil
{
    public JwtUtil()
    {
        
    }
    public static string GenerateJwtToken(User user, Tuple<string, Guid>? guidClaim, IConfiguration configuration)
    {
        string secret = configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret is missing in configuration.");
        string issuer = configuration["JWT:Issuer"] ?? throw new InvalidOperationException("JWT:Issuer is missing in configuration.");

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
            new Claim("email", user.Email ?? "")
        };

        // Add all roles
        if (user.UserRoles != null)
        {
            foreach (var role in user.UserRoles)
            {
                if (!string.IsNullOrEmpty(role.Role?.RoleName))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.Role.RoleName));
                }
            }
        }

        // Add custom GUID claim (if needed)
        if (guidClaim != null && !claims.Any(c => c.Type == guidClaim.Item1))
        {
            claims.Add(new Claim(guidClaim.Item1, guidClaim.Item2.ToString()));
        }
        
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: null, // you can set this if needed
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

    public static ClaimsPrincipal GetPrincipalFromExpiredToken(string token, IConfiguration configuration)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = configuration["JWT:Issuer"],
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"]!)),
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken securityToken;
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
        var jwtSecurityToken = securityToken as JwtSecurityToken;
        if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256Signature, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new SecurityTokenException("Invalid token");
        }
        return principal;
    }
    
}