using System.ComponentModel.DataAnnotations;

namespace ChatBox.Domain.Models
{
    /// <summary>
    /// Represents user-specific chat preferences and settings
    /// </summary>
    public class UserPreference 
    {

        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Language { get; set; } = "en";
        public string ResponseStyle { get; set; } = "balanced";
        public string Tone { get; set; } = "professional";
        public int MaxResponseLength { get; set; } = 500;
        public bool IncludeCitations { get; set; } = true;
        public bool EnableSuggestions { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        public string TimeZone { get; set; } = "UTC";
        public string DateFormat { get; set; } = "MM/dd/yyyy";
        public string Theme { get; set; } = "light";
        public string CustomSettings { get; set; } // JSON string
        public string PreferredTopics { get; set; } // JSON string
        public string BlockedTopics { get; set; } // JSON string
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
