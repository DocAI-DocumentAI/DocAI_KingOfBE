namespace ChatBox.API.Payload.Response
{
    public class UserPreferenceResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string? SessionId { get; set; } // NULL = User-level, NOT NULL = Session-specific
        public string UserName { get; set; } = string.Empty;
        public List<string> ChatbotCharacteristics { get; set; } = new();
        public string AdditionalInfo { get; set; } = string.Empty;
        public bool ApplyToNewChats { get; set; } = false;
        public bool HasAnyPreferences { get; set; }
    }
}
