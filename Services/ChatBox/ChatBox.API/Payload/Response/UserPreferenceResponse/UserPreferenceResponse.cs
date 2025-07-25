namespace ChatBox.API.Payload.Response.UserPreferenceResponse
{
    public class UserPreferenceResponse
    {
        public Guid UserId { get; set; }
        public string Language { get; set; }
        public string ResponseStyle { get; set; }
        public string Tone { get; set; }
        public int MaxResponseLength { get; set; }
        public bool IncludeCitations { get; set; }
        public bool EnableSuggestions { get; set; }
        public bool EnableNotifications { get; set; }
        public string TimeZone { get; set; }
        public string DateFormat { get; set; }
        public string Theme { get; set; }
        public Dictionary<string, object> CustomSettings { get; set; } = new();
        public List<string> PreferredTopics { get; set; } = new();
        public List<string> BlockedTopics { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDefault { get; set; }
        public PreferenceValidationInfo ValidationInfo { get; set; }
    }
}