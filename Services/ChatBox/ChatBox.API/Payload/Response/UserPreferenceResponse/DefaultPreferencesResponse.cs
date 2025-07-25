namespace ChatBox.API.Payload.Response.UserPreferenceResponse
{
    public class DefaultPreferencesResponse
    {
        public Dictionary<string, PreferenceDefault> Defaults { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public string Version { get; set; }
        public List<PreferenceCategory> Categories { get; set; } = new();
    }
}
