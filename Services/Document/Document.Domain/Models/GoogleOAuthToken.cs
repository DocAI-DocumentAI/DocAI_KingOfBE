using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Document.Domain.Models
{
    [Table("GoogleOAuthTokens")]
    public class GoogleOAuthToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string TokenType { get; set; } = string.Empty; // "company" or user email

        [Required]
        [MaxLength(2048)]
        public string AccessToken { get; set; } = string.Empty;

        [Required]
        [MaxLength(512)]
        public string RefreshToken { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }

        // Optional: Store additional metadata
        [MaxLength(100)]
        public string? UserEmail { get; set; }

        [MaxLength(100)]
        public string? Scope { get; set; }

        // Ensure only one token per type/user
        public static string GetCompanyTokenType() => "company";
        public static string GetUserTokenType(string email) => $"user:{email}";
    }
}
