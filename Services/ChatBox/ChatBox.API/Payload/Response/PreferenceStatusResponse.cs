namespace ChatBox.API.Payload.Response
{
    public class PreferenceStatusResponse
    {
        public string CurrentSource { get; set; } = "None"; // "UserDefault", "SessionOverride", "None"
        public string DisplayName { get; set; } = "User";
        public string StatusBadge { get; set; } = "⚪ Chưa thiết lập";
        public string StatusColor { get; set; } = "gray"; // "green", "blue", "gray"
        public bool HasOverride { get; set; } = false;
        public List<string> CurrentCharacteristics { get; set; } = new();
        public string CurrentAdditionalInfo { get; set; } = "";
        public UserDefaultInfo? FallbackUserDefault { get; set; }
    }
}
