namespace ChatBox.API.Payload.Response.UserPreferenceResponse
{
    public class PreferenceCategory
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int SortOrder { get; set; }
        public List<string> Settings { get; set; } = new();
    }
}
