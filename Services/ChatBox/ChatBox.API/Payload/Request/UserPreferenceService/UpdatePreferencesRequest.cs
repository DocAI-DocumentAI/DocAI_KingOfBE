namespace ChatBox.API.Payload.Request.UserPreferenceService
{
    public class UpdatePreferencesRequest
    {
        public string Language { get; set; }
        public string ResponseStyle { get; set; } // concise, balanced, detailed
        public string Tone { get; set; } // formal, professional, friendly, casual
        public int MaxResponseLength { get; set; } = 500;
        public bool IncludeCitations { get; set; } = true;
        public bool EnableSuggestions { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        public string TimeZone { get; set; }
        public string DateFormat { get; set; }
        public string Theme { get; set; } // light, dark, auto
        public Dictionary<string, object> CustomSettings { get; set; } = new();
        public List<string> PreferredTopics { get; set; } = new();
        public List<string> BlockedTopics { get; set; } = new();
    }
}
